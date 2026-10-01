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
    /// Declares which recurring handlers exist, which are enabled, and which wait for which, and turns that into a
    /// checked, immutable <see cref="ExecutionPlan"/>.
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
    /// planner.Add&lt;BrandHandler&gt;().Enabled(isBrandEnabled);
    /// planner.Add&lt;ProductHandler&gt;().DependsOn&lt;BrandHandler&gt;();
    /// var plan = planner.Build();
    /// </code>
    /// </example>
    public sealed class ExecutionPlanner
    {
        internal static readonly IEqualityComparer<Type> TypeComparer = new TypeEqualityComparer();

        private static readonly IReadOnlyList<Type> None = new ReadOnlyCollection<Type>(new Type[0]);

        private readonly Dictionary<Type, ExecutionPlanEntry> _entries = new Dictionary<Type, ExecutionPlanEntry>(TypeComparer);
        private readonly List<ExecutionPlanEntry> _entryOrder = new List<ExecutionPlanEntry>();
        private bool _built;

        /// <summary>Gets the declared entries, in declaration order.</summary>
        public IReadOnlyList<ExecutionPlanEntry> Entries => _entryOrder;

        /// <summary>
        /// Declares a recurring handler and returns its entry, to chain <see cref="ExecutionPlanEntry.DependsOn{TPrerequisite}"/>,
        /// <see cref="ExecutionPlanEntry.Enabled(bool)"/> and <see cref="ExecutionPlanEntry.DisplayName(string)"/>.
        /// </summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        public ExecutionPlanEntry Add<THandler>() where THandler : class, IHandler
        {
            return Add(typeof(THandler));
        }

        /// <summary>
        /// Declares a recurring handler and returns its entry. Prerequisites declared with <see cref="DependsOnAttribute"/>
        /// on the class are added to it.
        /// </summary>
        /// <param name="handlerType">The recurring handler type, implementing <see cref="IHandler"/>.</param>
        /// <exception cref="ArgumentException">The type doesn't implement <see cref="IHandler"/>.</exception>
        /// <exception cref="InvalidOperationException">The handler was already added, depends on itself, or the planner was already built.</exception>
        public ExecutionPlanEntry Add(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            ThrowIfBuilt();

            if (!typeof(IHandler).IsAssignableFrom(handlerType))
                throw new ArgumentException(
                    $"'{handlerType.FullName}' is not a recurring handler: only IHandler implementations can be in the execution plan.", nameof(handlerType));

            if (_entries.ContainsKey(handlerType))
                throw new InvalidOperationException($"'{handlerType.FullName}' was added to the execution plan twice.");

            var entry = new ExecutionPlanEntry(this, handlerType);
            foreach (var attribute in handlerType.GetCustomAttributes<DependsOnAttribute>(false))
                entry.DependsOn(attribute.Prerequisite);

            _entries.Add(handlerType, entry);
            _entryOrder.Add(entry);
            return entry;
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

            ValidateRunModes();
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
                    entry.ConfiguredMinInterval,
                    entry.RunsOnOwnSchedule))
                .ToList();

            return new ExecutionPlan(steps);
        }

        internal void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("The execution planner was already built and can no longer be changed.");
        }

        /// <summary>
        /// Walks the steps in execution order, so every prerequisite is resolved before its dependents and a skip cascades
        /// in a single pass.
        /// </summary>
        private Dictionary<Type, string> ResolveSkipReasons(List<ExecutionPlanEntry> order, MissingPrerequisite policy)
        {
            var reasons = new Dictionary<Type, string>(TypeComparer);

            foreach (var entry in order)
            {
                if (!entry.IsEnabled)
                {
                    reasons.Add(entry.HandlerType, "disabled");
                    continue;
                }

                string reason = null;
                foreach (var prerequisite in entry.Prerequisites)
                {
                    var unavailable = !_entries.ContainsKey(prerequisite)
                        ? "is not in the execution plan"
                        : reasons[prerequisite] != null ? "is not enabled (" + reasons[prerequisite] + ")" : null;

                    if (unavailable == null)
                        continue;

                    // The worst outcome here is silent: the dependent would wait for a cycle that never comes.
                    if (policy == MissingPrerequisite.Throw)
                        throw new InvalidOperationException(
                            $"'{entry.HandlerType.FullName}' is enabled and depends on '{prerequisite.FullName}', which {unavailable}. " +
                            $"Add or enable '{prerequisite.Name}', disable '{entry.HandlerType.Name}', or remove the dependency.");

                    reason = $"prerequisite '{prerequisite.Name}' {unavailable}";
                    break;
                }

                reasons.Add(entry.HandlerType, reason);
            }

            return reasons;
        }

        /// <summary>The run modes only apply to dependents, and only one at a time.</summary>
        private void ValidateRunModes()
        {
            foreach (var entry in _entryOrder)
            {
                var hasMinInterval = entry.ConfiguredMinInterval.HasValue;
                if (!hasMinInterval && !entry.RunsOnOwnSchedule)
                    continue;

                if (hasMinInterval && entry.RunsOnOwnSchedule)
                    throw new InvalidOperationException(
                        $"'{entry.HandlerType.FullName}' sets both MinInterval and RunOnOwnSchedule. With its own schedule, the schedule already spaces its runs: use one of them.");

                if (entry.Prerequisites.Count == 0)
                    throw new InvalidOperationException(
                        $"'{entry.HandlerType.FullName}' sets {(hasMinInterval ? "MinInterval" : "RunOnOwnSchedule")} but has no prerequisites, so nothing triggers it. Add a dependency or remove the setting.");
            }
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
        private List<ExecutionPlanEntry> Order()
        {
            var order = new List<ExecutionPlanEntry>();
            var visited = new HashSet<Type>(TypeComparer);

            foreach (var entry in _entryOrder)
                VisitForOrder(entry, visited, order);

            return order;
        }

        private void VisitForOrder(ExecutionPlanEntry entry, HashSet<Type> visited, List<ExecutionPlanEntry> order)
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

        private static string ResolveDisplayName(ExecutionPlanEntry entry)
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
