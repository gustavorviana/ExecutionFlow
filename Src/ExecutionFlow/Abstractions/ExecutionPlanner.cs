using ExecutionFlow.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Configures the recurring handlers, which are enabled, and which wait for which, and turns that into a checked,
    /// immutable <see cref="ExecutionPlan"/>. It's the single place where handlers are configured: inline with
    /// <see cref="Add{THandler}"/>, or with <see cref="IHandlerConfiguration{THandler}"/> classes.
    /// </summary>
    /// <remarks>
    /// <para><b>A plan, not a list.</b> Registering handler by handler says what runs, but not in what order. The order is
    /// a data requirement when one handler's output references another's. The plan declares that once, in one place, and
    /// checks it before the process starts.</para>
    /// <para><b>No host needed.</b> <see cref="Build(MissingPrerequisite)"/> only looks at types, so a cycle or an
    /// unavailable prerequisite fails at startup, readably, instead of turning into a handler that silently never runs.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var planner = new ExecutionPlanner();
    /// planner.ApplyConfigurationsFromAssembly(typeof(ProductSyncConfiguration).Assembly);
    /// planner.Add&lt;BrandHandler&gt;().Enabled(isBrandEnabled);
    /// planner.Add&lt;PriceHandler&gt;().DependsOn&lt;BrandHandler&gt;().Cron("*/10 * * * *");
    /// var plan = planner.Build();
    /// </code>
    /// </example>
    public sealed class ExecutionPlanner
    {
        internal static readonly IEqualityComparer<Type> TypeComparer = new TypeEqualityComparer();

        private static readonly IReadOnlyList<Type> None = new ReadOnlyCollection<Type>(new Type[0]);

        private readonly Dictionary<Type, HandlerBuilder> _entries = new Dictionary<Type, HandlerBuilder>(TypeComparer);
        private readonly List<HandlerBuilder> _entryOrder = new List<HandlerBuilder>();
        private bool _built;

        /// <summary>Gets the configured handlers, in declaration order.</summary>
        public IReadOnlyList<HandlerBuilder> Entries => _entryOrder;

        /// <summary>
        /// Declares a recurring handler and returns its builder, the same one an <see cref="IHandlerConfiguration{THandler}"/>
        /// receives. Prerequisites declared with <see cref="DependsOnAttribute"/> on the class are added to it.
        /// </summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <exception cref="InvalidOperationException">The handler was already configured, or the planner was already built.</exception>
        public HandlerBuilder<THandler> Add<THandler>() where THandler : class, IHandler
        {
            return (HandlerBuilder<THandler>)Register(new HandlerBuilder<THandler>(this));
        }

        /// <summary>
        /// Declares a recurring handler and returns its builder. Prerequisites declared with <see cref="DependsOnAttribute"/>
        /// on the class are added to it.
        /// </summary>
        /// <param name="handlerType">The recurring handler type, implementing <see cref="IHandler"/>.</param>
        /// <exception cref="ArgumentException">The type doesn't implement <see cref="IHandler"/>.</exception>
        /// <exception cref="InvalidOperationException">The handler was already configured, depends on itself, or the planner was already built.</exception>
        public HandlerBuilder Add(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            if (!typeof(IHandler).IsAssignableFrom(handlerType))
                throw new ArgumentException(
                    $"'{handlerType.FullName}' is not a recurring handler: only IHandler implementations can be in the execution plan.", nameof(handlerType));

            var builderType = typeof(HandlerBuilder<>).MakeGenericType(handlerType);
            var builder = (HandlerBuilder)Activator.CreateInstance(builderType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { this }, null);
            return Register(builder);
        }

        /// <summary>Configures a handler with a configuration class, as <c>configuration.Configure(Add&lt;THandler&gt;())</c>.</summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <returns>This planner, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The handler was already configured, or the planner was already built.</exception>
        public ExecutionPlanner ApplyConfiguration<THandler>(IHandlerConfiguration<THandler> configuration) where THandler : class, IHandler
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            configuration.Configure(Add<THandler>());
            return this;
        }

        /// <summary>
        /// Creates and applies every non-abstract class in <paramref name="assembly"/> that implements
        /// <see cref="IHandlerConfiguration{THandler}"/>, with its parameterless constructor.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>This planner, for chaining.</returns>
        /// <exception cref="InvalidOperationException">A configuration class has no parameterless constructor, or a handler is configured twice.</exception>
        public ExecutionPlanner ApplyConfigurationsFromAssembly(Assembly assembly)
        {
            return ApplyConfigurationsFromAssembly(assembly, null);
        }

        /// <summary>
        /// Creates and applies the non-abstract classes in <paramref name="assembly"/> that implement
        /// <see cref="IHandlerConfiguration{THandler}"/> and match <paramref name="predicate"/>, as EF Core's overload does.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <param name="predicate">Filter on the configuration class; <c>null</c> applies all.</param>
        /// <returns>This planner, for chaining.</returns>
        /// <exception cref="InvalidOperationException">A configuration class has no parameterless constructor, or a handler is configured twice.</exception>
        public ExecutionPlanner ApplyConfigurationsFromAssembly(Assembly assembly, Func<Type, bool> predicate)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            ThrowIfBuilt();

            var apply = typeof(ExecutionPlanner).GetMethod(nameof(ApplyConfiguration));
            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters
                && (predicate == null || predicate(t))))
            {
                foreach (var configurationInterface in type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandlerConfiguration<>)))
                {
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        throw new InvalidOperationException(
                            $"Handler configuration '{type.FullName}' has no parameterless constructor, so it can't be applied from the assembly.");

                    var handlerType = configurationInterface.GetGenericArguments()[0];
                    try
                    {
                        apply.MakeGenericMethod(handlerType).Invoke(this, new[] { Activator.CreateInstance(type) });
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException != null)
                    {
                        throw ex.InnerException;
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Checks the plan and returns it frozen. An enabled handler with an unavailable prerequisite (not added, or
        /// disabled) throws.
        /// </summary>
        /// <exception cref="InvalidOperationException">The plan has a cycle or an unavailable prerequisite, or was already built.</exception>
        public ExecutionPlan Build()
        {
            return Build(MissingPrerequisite.Throw);
        }

        /// <summary>
        /// Checks the plan and returns it frozen. After this call the planner can't be changed.
        /// </summary>
        /// <param name="missingPrerequisite">What to do when an enabled handler has an unavailable prerequisite.</param>
        /// <exception cref="InvalidOperationException">The plan has a cycle, or an unavailable prerequisite under <see cref="MissingPrerequisite.Throw"/>, or was already built.</exception>
        public ExecutionPlan Build(MissingPrerequisite missingPrerequisite)
        {
            ThrowIfBuilt();
            _built = true;

            var runModes = ResolveRunModes();
            DetectCycles();

            var order = Order();
            var dependents = CollectDependents();
            var reasons = ResolveSkipReasons(order, missingPrerequisite);

            var steps = order
                .Select(entry => new ExecutionPlanStep(
                    entry.HandlerType,
                    ResolveDisplayName(entry),
                    reasons[entry.HandlerType],
                    ReadOnly(entry.Prerequisites),
                    dependents.TryGetValue(entry.HandlerType, out var list) ? ReadOnly(list) : None,
                    runModes[entry.HandlerType].MinInterval,
                    runModes[entry.HandlerType].Mode == DependentRunMode.OwnSchedule,
                    entry.IsOverlapConfigured ? entry.ConfiguredOverlapTimeout : NoOverlapAttribute.GetTimeout(entry.HandlerType),
                    entry.ConfiguredCron,
                    entry.ConfiguredInterval,
                    entry.ConfiguredId,
                    entry.ConfiguredTimeZone,
                    entry.ConfiguredEnabled,
                    new ReadOnlyDictionary<string, object>(entry.Settings.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal))))
                .ToList();

            return new ExecutionPlan(steps, missingPrerequisite);
        }

        /// <summary>Whether the handler was already declared in this planner.</summary>
        internal bool Contains(Type handlerType) => _entries.ContainsKey(handlerType);

        /// <summary>Puts the declared handlers in <paramref name="order"/>, keeping the others after them.</summary>
        internal void Reorder(IReadOnlyList<Type> order)
        {
            var position = new Dictionary<Type, int>(TypeComparer);
            for (var i = 0; i < order.Count; i++)
                if (!position.ContainsKey(order[i]))
                    position.Add(order[i], i);

            var sorted = _entryOrder
                .Select((entry, index) => (entry, key: position.TryGetValue(entry.HandlerType, out var p) ? p : order.Count + index))
                .OrderBy(x => x.key)
                .Select(x => x.entry)
                .ToList();

            _entryOrder.Clear();
            _entryOrder.AddRange(sorted);
        }

        internal void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("The execution planner was already built and can no longer be changed.");
        }

        private HandlerBuilder Register(HandlerBuilder builder)
        {
            ThrowIfBuilt();
            var handlerType = builder.HandlerType;

            if (_entries.ContainsKey(handlerType))
                throw new InvalidOperationException(
                    $"'{handlerType.FullName}' was configured twice in the execution plan (two configuration classes, or a configuration and Add). Configure it in one place.");

            foreach (var attribute in handlerType.GetCustomAttributes<DependsOnAttribute>(false))
                builder.DependsOn(attribute.Prerequisite);
            builder.RunModeAttribute = handlerType.GetCustomAttribute<DependentRunAttribute>(false);
            builder.HasExplicitValues = false;

            _entries.Add(handlerType, builder);
            _entryOrder.Add(builder);
            return builder;
        }

        /// <summary>
        /// Walks the steps in execution order, so every prerequisite is resolved before its dependents and a skip cascades
        /// in a single pass.
        /// </summary>
        private static Dictionary<Type, string> ResolveSkipReasons(List<HandlerBuilder> order, MissingPrerequisite policy)
        {
            var reasons = new Dictionary<Type, string>(TypeComparer);

            foreach (var entry in order)
            {
                var reason = entry.IsEnabled
                    ? ResolveUnavailablePrerequisite(entry.HandlerType, entry.Prerequisites, reasons, policy)
                    : "disabled";
                reasons.Add(entry.HandlerType, reason);
            }

            return reasons;
        }

        /// <summary>
        /// The skip reason of an enabled handler, or <c>null</c> when every prerequisite is available. <paramref name="resolved"/>
        /// holds the handlers already resolved in execution order, so a prerequisite missing from it isn't in the plan.
        /// </summary>
        internal static string ResolveUnavailablePrerequisite(Type handlerType, IReadOnlyList<Type> prerequisites,
            Dictionary<Type, string> resolved, MissingPrerequisite policy)
        {
            foreach (var prerequisite in prerequisites)
            {
                var unavailable = !resolved.TryGetValue(prerequisite, out var prerequisiteReason)
                    ? "is not in the execution plan"
                    : prerequisiteReason != null ? "is not enabled (" + prerequisiteReason + ")" : null;

                if (unavailable == null)
                    continue;

                // The worst outcome here is silent: the dependent would wait for a cycle that never comes.
                if (policy == MissingPrerequisite.Throw)
                    throw new InvalidOperationException(
                        $"'{handlerType.FullName}' is enabled and depends on '{prerequisite.FullName}', which {unavailable}. " +
                        $"Add or enable '{prerequisite.Name}', disable '{handlerType.Name}', or remove the dependency.");

                return $"prerequisite '{prerequisite.Name}' {unavailable}";
            }

            return null;
        }

        /// <summary>Resolves each handler's run mode (builder, else attribute) and checks it only applies to dependents.</summary>
        private Dictionary<Type, (DependentRunMode? Mode, TimeSpan? MinInterval)> ResolveRunModes()
        {
            var modes = new Dictionary<Type, (DependentRunMode? Mode, TimeSpan? MinInterval)>(TypeComparer);

            foreach (var entry in _entryOrder)
            {
                var mode = entry.ResolveRunMode(out var minInterval);
                if (mode.HasValue && entry.Prerequisites.Count == 0)
                    throw new InvalidOperationException(
                        $"'{entry.HandlerType.FullName}' sets the run mode {mode.Value} but has no prerequisites, so nothing triggers it. Add a dependency or remove the run mode.");

                modes.Add(entry.HandlerType, (mode, minInterval));
            }

            return modes;
        }

        /// <summary>
        /// Depth-first search with three states. A cycle would be a deadlock at run time: handlers waiting for each other
        /// forever, with no exception and no error log. Prerequisites outside the plan can't close a cycle and are skipped.
        /// </summary>
        private void DetectCycles()
        {
            var done = new Dictionary<Type, bool>(TypeComparer); // false = visiting, true = done
            var path = new List<Type>();

            foreach (var entry in _entryOrder)
                Visit(entry.HandlerType, done, path);
        }

        private void Visit(Type handlerType, Dictionary<Type, bool> state, List<Type> path)
        {
            if (state.TryGetValue(handlerType, out var done))
            {
                if (done)
                    return;

                // Back at a handler that is still being visited: the path closed a cycle.
                var start = path.FindIndex(t => TypeComparer.Equals(t, handlerType));
                var cycle = path.Skip(start).Concat(new[] { handlerType }).Select(t => t.Name);
                throw new InvalidOperationException(
                    $"Circular dependency in the execution plan: {string.Join(" -> ", cycle)}. Remove one of these dependencies.");
            }

            state[handlerType] = false;
            path.Add(handlerType);

            foreach (var prerequisite in _entries[handlerType].Prerequisites)
                if (_entries.ContainsKey(prerequisite))
                    Visit(prerequisite, state, path);

            path.RemoveAt(path.Count - 1);
            state[handlerType] = true;
        }

        /// <summary>Post-order DFS over the declaration order: every prerequisite comes before its dependents.</summary>
        private List<HandlerBuilder> Order()
        {
            var order = new List<HandlerBuilder>();
            var visited = new HashSet<Type>(TypeComparer);

            foreach (var entry in _entryOrder)
                VisitForOrder(entry, visited, order);

            return order;
        }

        private void VisitForOrder(HandlerBuilder entry, HashSet<Type> visited, List<HandlerBuilder> order)
        {
            if (!visited.Add(entry.HandlerType))
                return;

            foreach (var prerequisite in entry.Prerequisites)
                if (_entries.TryGetValue(prerequisite, out var prerequisiteEntry))
                    VisitForOrder(prerequisiteEntry, visited, order);

            order.Add(entry);
        }

        private Dictionary<Type, List<Type>> CollectDependents()
        {
            var dependents = new Dictionary<Type, List<Type>>(TypeComparer);

            foreach (var entry in _entryOrder)
            {
                foreach (var prerequisite in entry.Prerequisites)
                {
                    if (!dependents.TryGetValue(prerequisite, out var list))
                        dependents.Add(prerequisite, list = new List<Type>());
                    list.Add(entry.HandlerType);
                }
            }

            return dependents;
        }

        private static string ResolveDisplayName(HandlerBuilder entry)
        {
            return entry.ConfiguredDisplayName
                ?? entry.HandlerType.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                ?? entry.HandlerType.Name;
        }

        private static IReadOnlyList<Type> ReadOnly(IEnumerable<Type> types)
        {
            return new ReadOnlyCollection<Type>(types.ToArray());
        }
    }
}
