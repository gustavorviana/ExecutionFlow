using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace ExecutionFlow
{
    /// <summary>
    /// Base configuration class for registering handlers, event types, and logger factories.
    /// Options are locked after <see cref="Abstractions.ExecutionFlowSetup{TOptions}.Configure"/> completes.
    /// </summary>
    public abstract class ExecutionFlowOptions
    {
        private bool _locked;

        /// <summary>
        /// Optional callback invoked when a <see cref="ReflectionTypeLoadException"/> occurs during assembly scanning.
        /// </summary>
        public Action<AssemblyTypeScanContext> OnTypeLoadFailure { get => _onTypeLoadFailure; set { ThrowIfLocked(); _onTypeLoadFailure = value; } }
        private Action<AssemblyTypeScanContext> _onTypeLoadFailure;

        /// <summary>
        /// Gets or sets the default deduplication behavior for events with a custom ID. Default is <see cref="DeduplicationBehavior.Disabled"/>.
        /// An event type can override it with <see cref="Attributes.DeduplicationAttribute"/>.
        /// </summary>
        public DeduplicationBehavior DeduplicationBehavior { get => _deduplicationBehavior; set { ThrowIfLocked(); _deduplicationBehavior = value; } }
        private DeduplicationBehavior _deduplicationBehavior = DeduplicationBehavior.Disabled;

        private readonly List<Type> _loggerFactoryTypes = new List<Type>();
        private readonly Dictionary<Type, RecurringJobRegistryInfo> _recurringHandlers = new Dictionary<Type, RecurringJobRegistryInfo>(new TypeEqualityComparer());
        private readonly List<Type> _recurringOrder = new List<Type>();
        private readonly Dictionary<Type, EventJobRegistryInfo> _eventHandlers = new Dictionary<Type, EventJobRegistryInfo>(new TypeEqualityComparer());

        /// <summary>Gets the registered logger factory types.</summary>
        public IReadOnlyList<Type> LoggerFactoryTypes => _loggerFactoryTypes;

        /// <summary>Gets the registered recurring handlers, keyed by handler type.</summary>
        public IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers => _recurringHandlers;

        /// <summary>Gets the registered recurring handler types, in the order they were first registered.</summary>
        internal IReadOnlyList<Type> RecurringHandlerOrder => _recurringOrder;

        /// <summary>Gets the registered event handlers, keyed by event type.</summary>
        public IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers => _eventHandlers;

        /// <summary>
        /// Scans an assembly for all handler implementations and registers them.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void Scan(Assembly assembly)
        {
            Scan(assembly, null);
        }

        /// <summary>
        /// Scans an assembly for handler implementations matching the predicate and registers them.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <param name="predicate">Optional filter to include only matching types. Pass <c>null</c> to include all.</param>
        public void Scan(Assembly assembly, Func<Type, bool> predicate)
        {
            ThrowIfLocked();

            foreach (var type in GetValidTypes(assembly))
                if (!type.IsAbstract && !type.IsInterface && ImplementsHandler(type) && (predicate == null || predicate(type)))
                    Add(type);
        }

        private Type[] GetValidTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loadedTypes = ex.Types
                    .Where(t => t != null && t.IsClass && !t.IsAbstract && ImplementsHandler(t))
                    .Cast<Type>()
                    .ToArray();

                OnTypeLoadFailure?.Invoke(new AssemblyTypeScanContext(assembly, ex, loadedTypes));

                return loadedTypes;
            }
        }

        private static bool ImplementsHandler(Type type)
        {
            if (typeof(IHandler).IsAssignableFrom(type))
                return true;

            return type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>));
        }

        /// <summary>
        /// Registers a handler type. The type must implement <see cref="IHandler"/> or <see cref="IHandler{TEvent}"/> (but not both).
        /// </summary>
        /// <param name="handlerType">The handler type to register.</param>
        /// <exception cref="ArgumentException">Thrown if the type does not implement a handler interface.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the type implements both interfaces, multiple event types, or a duplicate event handler.</exception>
        public void Add(Type handlerType)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            var recurringAttr = handlerType.GetCustomAttribute<Attributes.RecurringAttribute>();
            var displayNameAttr = handlerType.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
            var displayName = displayNameAttr?.DisplayName ?? handlerType.Name;
            var cron = recurringAttr?.Cron;

            var isRecurring = typeof(IHandler).IsAssignableFrom(handlerType);
            var eventInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>))
                .ToArray();

            if (isRecurring && eventInterfaces.Length > 0)
                throw new InvalidOperationException(
                    $"Type '{handlerType.FullName}' implements both IHandler and IHandler<TEvent>. A handler must implement only one of them.");

            if (eventInterfaces.Length > 1)
                throw new InvalidOperationException(
                    $"Type '{handlerType.FullName}' implements IHandler<TEvent> for multiple event types. A handler must handle a single event type.");

            if (isRecurring)
            {
                var intervalAttr = handlerType.GetCustomAttribute<Attributes.RecurringIntervalAttribute>();
                var interval = intervalAttr == null ? (TimeSpan?)null : ParseInterval(handlerType, intervalAttr.Interval);

                if (intervalAttr != null && recurringAttr != null)
                    throw new InvalidOperationException(
                        $"Recurring handler '{handlerType.FullName}' has both [Recurring] and [RecurringInterval]. Use only one schedule.");

                // Re-adding a handler keeps its first position: the automatic plan follows the registration order.
                if (!_recurringHandlers.ContainsKey(handlerType))
                    _recurringOrder.Add(handlerType);

                _recurringHandlers[handlerType] = new RecurringJobRegistryInfo(
                    handlerType: handlerType,
                    displayName: displayName,
                    cron: cron,
                    id: recurringAttr?.Id ?? intervalAttr?.Id,
                    timeZone: recurringAttr?.TimeZone,
                    interval: interval,
                    overlapTimeout: Attributes.NoOverlapAttribute.GetTimeout(handlerType)
                );
                return;
            }

            if (eventInterfaces.Length == 1)
            {
                var eventType = eventInterfaces[0].GetGenericArguments()[0];

                if (_eventHandlers.TryGetValue(eventType, out var existingHandler) && existingHandler.HandlerType != handlerType)
                    throw new InvalidOperationException(
                        $"An event handler for event type '{eventType.FullName}' is already registered (existing: '{existingHandler.HandlerType.FullName}', duplicate: '{handlerType.FullName}').");

                _eventHandlers[eventType] = new EventJobRegistryInfo(
                    handlerType: handlerType,
                    eventType: eventType,
                    displayName: displayName
                );
                return;
            }

            throw new ArgumentException(
                $"Type '{handlerType.FullName}' does not implement IHandler or IHandler<TEvent>.", nameof(handlerType));
        }

        private static TimeSpan ParseInterval(Type handlerType, string value)
        {
            if (!Attributes.TimeSpanText.TryParse(value, out var interval))
                throw new InvalidOperationException(
                    $"Recurring handler '{handlerType.FullName}' has an invalid interval '{value}'. Use the format [d.]hh:mm:ss, e.g. \"00:15:00\".");

            if (interval < TimeSpan.FromMinutes(1))
                throw new InvalidOperationException(
                    $"Recurring handler '{handlerType.FullName}' has the interval '{value}', but the minimum is one minute.");

            return interval;
        }

        /// <summary>
        /// Gets or sets what the automatic plan does when a handler depends on one that isn't registered or is turned off.
        /// Default: <see cref="MissingPrerequisite.Throw"/>. It only applies to the automatic plan, formed from
        /// <see cref="Attributes.DependsOnAttribute"/> and <see cref="Attributes.DependentRunAttribute"/> when
        /// <see cref="UsePlan"/> isn't called; an explicit plan takes its policy from <see cref="ExecutionPlanner.Build(MissingPrerequisite)"/>.
        /// </summary>
        public MissingPrerequisite AutomaticPlanMissingPrerequisite { get => _automaticPlanMissingPrerequisite; set { ThrowIfLocked(); _automaticPlanMissingPrerequisite = value; } }
        private MissingPrerequisite _automaticPlanMissingPrerequisite = MissingPrerequisite.Throw;

        /// <summary>Gets the execution plan registered with <see cref="UsePlan"/>, the automatic plan, or <c>null</c>.</summary>
        public ExecutionPlan Plan { get; private set; }

        /// <summary>
        /// Registers the execution plan built with <see cref="ExecutionPlanner"/>. The options only store it; each execution
        /// provider applies it (see the provider's documentation).
        /// </summary>
        /// <param name="plan">The built plan.</param>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">A plan was already registered, or the provider rejects the plan.</exception>
        public void UsePlan(ExecutionPlan plan)
        {
            ThrowIfLocked();
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (Plan != null) throw new InvalidOperationException("An execution plan was already registered.");

            OnPlanRegistering(plan);
            Plan = plan;
        }

        /// <summary>
        /// Shortcut for registering a plan made only of configuration classes: builds an <see cref="ExecutionPlanner"/> from
        /// the <see cref="IHandlerConfiguration{THandler}"/> classes of <paramref name="assembly"/> that match
        /// <paramref name="predicate"/>, and registers it with <see cref="UsePlan"/>. Use the planner directly to mix them
        /// with inline configuration. Each provider gets its own plan: filter the classes that belong to it.
        /// </summary>
        /// <param name="assembly">The assembly with the configuration classes.</param>
        /// <param name="predicate">Filter on the configuration classes (e.g. by namespace); <c>null</c> applies all.</param>
        /// <param name="policy">What to do when a handler depends on one that isn't configured or is disabled.</param>
        /// <exception cref="InvalidOperationException">A plan was already registered, or the plan is invalid.</exception>
        public void UsePlanFromAssembly(Assembly assembly, Func<Type, bool> predicate = null, MissingPrerequisite policy = MissingPrerequisite.Throw)
        {
            ThrowIfLocked();
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (Plan != null) throw new InvalidOperationException("An execution plan was already registered.");

            UsePlan(new ExecutionPlanner().ApplyConfigurationsFromAssembly(assembly, predicate).Build(policy));
        }

        /// <summary>
        /// Called by <see cref="UsePlan"/> before the plan is stored, so a provider can validate and apply it.
        /// Throwing leaves no plan registered.
        /// </summary>
        /// <param name="plan">The plan being registered.</param>
        protected virtual void OnPlanRegistering(ExecutionPlan plan)
        {
        }

        // The internal planner: the class's attributes plus GetPlanOf. It's never built directly; without a user plan it
        // forms the automatic plan, otherwise its GetPlanOf values only apply to handlers outside the user plan.
        private readonly ExecutionPlanner _internalPlanner = new ExecutionPlanner();
        private readonly Dictionary<Type, HandlerBuilder> _optionsBuilders = new Dictionary<Type, HandlerBuilder>(new TypeEqualityComparer());

        /// <summary>
        /// Gets the builder of <typeparamref name="THandler"/> in these options, to configure it like in a plan
        /// (<see cref="HandlerBuilder{THandler}"/>). Its values win over the class's attributes. When the handler is in a plan
        /// registered with <see cref="UsePlan"/>, that plan's configuration replaces these values as a whole (attributes still
        /// apply), and configuration writes a trace warning. The handler must also be registered with <see cref="Scan(Assembly)"/>
        /// or <see cref="Add(Type)"/>.
        /// </summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <exception cref="InvalidOperationException">The options are locked.</exception>
        public HandlerBuilder<THandler> GetPlanOf<THandler>() where THandler : class, IHandler
        {
            return (HandlerBuilder<THandler>)GetPlanOf(typeof(THandler));
        }

        /// <summary>Gets the builder of <paramref name="handlerType"/> in these options; see <see cref="GetPlanOf{THandler}"/>.</summary>
        /// <param name="handlerType">The recurring handler type.</param>
        /// <exception cref="ArgumentException">The type doesn't implement <see cref="IHandler"/>.</exception>
        /// <exception cref="InvalidOperationException">The options are locked.</exception>
        public HandlerBuilder GetPlanOf(Type handlerType)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));

            if (!_optionsBuilders.TryGetValue(handlerType, out var builder))
            {
                builder = _internalPlanner.Add(handlerType);
                _optionsBuilders.Add(handlerType, builder);
            }

            return builder;
        }

        /// <summary>The provider's global on/off for recurring handlers, used when nothing more specific sets it. Default: <c>true</c>.</summary>
        protected virtual bool IsRecurringHandlerEnabledByDefault()
        {
            return true;
        }

        /// <summary>The provider's global time zone ID for cron schedules, or <c>null</c> for UTC. Default: <c>null</c>.</summary>
        protected virtual string DefaultRecurringTimeZone => null;

        /// <summary>
        /// Whether the recurring handler runs on its schedule: its plan step's resolved <see cref="ExecutionPlanStep.Enabled"/>
        /// when it's in the plan, else <see cref="GetPlanOf{THandler}"/>'s value, else the provider's global default.
        /// </summary>
        /// <param name="handlerType">The recurring handler type.</param>
        protected bool ResolveRecurringHandlerEnabled(Type handlerType)
        {
            if (Plan != null && Plan.TryGet(handlerType, out var step))
                return step.Enabled;

            return (_optionsBuilders.TryGetValue(handlerType, out var builder) ? builder.ConfiguredEnabled : null)
                ?? IsRecurringHandlerEnabledByDefault();
        }

        /// <summary>
        /// The time zone ID the handler's cron is evaluated in, or <c>null</c> for UTC: plan step (or, outside the plan,
        /// <see cref="GetPlanOf{THandler}"/>), then the attribute, then the provider's global default. An interval has none.
        /// </summary>
        /// <param name="registration">The handler's registration.</param>
        protected string ResolveRecurringTimeZoneId(RecurringJobRegistryInfo registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (registration.Interval.HasValue)
                return null;

            return GetConfiguredTimeZone(registration.HandlerType) ?? registration.TimeZone ?? DefaultRecurringTimeZone;
        }

        /// <summary>
        /// The time zone set specifically for the handler: its plan step's, or, outside the plan, <see cref="GetPlanOf{THandler}"/>'s;
        /// <c>null</c> when none (attributes and globals not included).
        /// </summary>
        /// <param name="handlerType">The recurring handler type.</param>
        protected string GetConfiguredTimeZone(Type handlerType)
        {
            if (Plan != null && Plan.TryGet(handlerType, out var step))
                return step.TimeZone;

            return _optionsBuilders.TryGetValue(handlerType, out var builder) ? builder.ConfiguredTimeZone : null;
        }

        /// <summary>The provider settings of the handler: its plan step's, or, outside the plan, <see cref="GetPlanOf{THandler}"/>'s.</summary>
        /// <param name="handlerType">The recurring handler type.</param>
        protected IReadOnlyDictionary<string, object> ResolveRecurringHandlerSettings(Type handlerType)
        {
            if (Plan != null && Plan.TryGet(handlerType, out var step))
                return step.Settings;

            return _optionsBuilders.TryGetValue(handlerType, out var builder) ? builder.Settings : EmptySettings;
        }

        private static readonly IReadOnlyDictionary<string, object> EmptySettings = new Dictionary<string, object>();

        private bool NeedsAutomaticPlan()
        {
            return _recurringOrder.Any(t =>
                    t.IsDefined(typeof(Attributes.DependsOnAttribute), false) || t.IsDefined(typeof(Attributes.DependentRunAttribute), false))
                || _optionsBuilders.Values.Any(b => b.Prerequisites.Count > 0 || b.ConfiguredRunMode.HasValue);
        }

        private void MergeRegistration(Type handlerType, string cron, TimeSpan? interval, string id, string displayName,
            bool overlapConfigured, TimeSpan? overlapTimeout)
        {
            if (!_recurringHandlers.TryGetValue(handlerType, out var registration))
                return;

            var resolvedCron = cron ?? (interval.HasValue ? null : registration.Cron);
            var resolvedInterval = interval ?? (cron != null ? null : registration.Interval);

            _recurringHandlers[handlerType] = new RecurringJobRegistryInfo(
                handlerType, displayName ?? registration.DisplayName, resolvedCron, id ?? registration.Id, registration.TimeZone,
                resolvedInterval, overlapConfigured ? overlapTimeout : registration.OverlapTimeout);
        }

        /// <summary>
        /// Applies the planners once the options are complete. With a user plan, its handlers drop their
        /// <see cref="GetPlanOf{THandler}"/> values (with a warning). Without one, the internal planner forms the automatic
        /// plan when it has dependencies or run modes. Plan steps (or, without a plan, <see cref="GetPlanOf{THandler}"/>)
        /// replace the attribute's schedule, ID and name in the registrations, and plan steps are re-resolved with
        /// <c>ConfiguredEnabled ?? global default</c>, applying the plan's <see cref="ExecutionPlan.MissingPrerequisite"/> again.
        /// </summary>
        internal void ResolvePlan()
        {
            foreach (var handlerType in _optionsBuilders.Keys)
            {
                if (!_recurringHandlers.ContainsKey(handlerType))
                    throw new InvalidOperationException(
                        $"GetPlanOf references type '{handlerType.FullName}' which is not registered as a recurring handler. Register it with Scan or Add.");
            }

            if (Plan != null)
            {
                foreach (var step in Plan.Steps)
                {
                    if (_optionsBuilders.TryGetValue(step.HandlerType, out var ignored) && ignored.HasExplicitValues)
                        Trace.TraceWarning(
                            "ExecutionFlow: '{0}' is in the execution plan, so its GetPlanOf configuration (including SetJobAutoRun and SetJobTimeZone) is ignored.",
                            step.HandlerType.FullName);
                }
            }
            else if (NeedsAutomaticPlan())
            {
                foreach (var handlerType in _recurringOrder)
                    if (!_internalPlanner.Contains(handlerType))
                        _internalPlanner.Add(handlerType);

                _internalPlanner.Reorder(_recurringOrder);
                Plan = _internalPlanner.Build(AutomaticPlanMissingPrerequisite);
            }
            else
            {
                foreach (var pair in _optionsBuilders)
                {
                    var b = pair.Value;
                    MergeRegistration(pair.Key, b.ConfiguredCron, b.ConfiguredInterval, b.ConfiguredId, b.ConfiguredDisplayName,
                        b.IsOverlapConfigured, b.ConfiguredOverlapTimeout);
                }

                return;
            }

            foreach (var step in Plan.Steps)
                MergeRegistration(step.HandlerType, step.Cron, step.Interval, step.Id, step.DisplayName, false, null);

            Plan = Plan.Reevaluate(_ => IsRecurringHandlerEnabledByDefault());
        }

        /// <summary>
        /// Registers a logger factory type.
        /// </summary>
        /// <typeparam name="TFactory">The logger factory type implementing <see cref="IExecutionLoggerFactory"/>.</typeparam>
        public void AddLogger<TFactory>() where TFactory : class, IExecutionLoggerFactory
        {
            AddLogger(typeof(TFactory));
        }

        /// <summary>
        /// Registers a logger factory type.
        /// </summary>
        /// <param name="factoryType">The type implementing <see cref="IExecutionLoggerFactory"/>.</param>
        public void AddLogger(Type factoryType)
        {
            ThrowIfLocked();

            if (factoryType == null) throw new ArgumentNullException(nameof(factoryType));
            if (!typeof(IExecutionLoggerFactory).IsAssignableFrom(factoryType))
                throw new ArgumentException($"Type '{factoryType.FullName}' does not implement IExecutionLoggerFactory.", nameof(factoryType));

            // Registering the same factory twice (e.g. ConfigureConsole() called twice) would write every line twice.
            if (!_loggerFactoryTypes.Contains(factoryType))
                _loggerFactoryTypes.Add(factoryType);
        }

        internal void Lock()
        {
            _locked = true;
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if the options have been locked after configuration.
        /// </summary>
        protected void ThrowIfLocked()
        {
            if (_locked)
                throw new InvalidOperationException("Options cannot be modified after Configure has completed.");
        }
    }
}
