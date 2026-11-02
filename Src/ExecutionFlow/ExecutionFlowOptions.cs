using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly Dictionary<Type, EventJobRegistryInfo> _eventHandlers = new Dictionary<Type, EventJobRegistryInfo>(new TypeEqualityComparer());

        /// <summary>Gets the registered logger factory types.</summary>
        public IReadOnlyList<Type> LoggerFactoryTypes => _loggerFactoryTypes;

        /// <summary>Gets the registered recurring handlers, keyed by handler type.</summary>
        public IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers => _recurringHandlers;

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

        private static readonly string[] IntervalFormats = { @"hh\:mm\:ss", @"d\.hh\:mm\:ss" };

        private static TimeSpan ParseInterval(Type handlerType, string value)
        {
            // Only hh:mm:ss with optional days: a bare number such as "10" would otherwise be read as days.
            if (!TimeSpan.TryParseExact(value ?? string.Empty, IntervalFormats, CultureInfo.InvariantCulture, out var interval))
                throw new InvalidOperationException(
                    $"Recurring handler '{handlerType.FullName}' has an invalid interval '{value}'. Use the format [d.]hh:mm:ss, e.g. \"00:15:00\".");

            if (interval < TimeSpan.FromMinutes(1))
                throw new InvalidOperationException(
                    $"Recurring handler '{handlerType.FullName}' has the interval '{value}', but the minimum is one minute.");

            return interval;
        }

        /// <summary>Gets the execution plan registered with <see cref="UsePlan"/>, or <c>null</c>.</summary>
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
        /// Called by <see cref="UsePlan"/> before the plan is stored, so a provider can validate and apply it.
        /// Throwing leaves no plan registered.
        /// </summary>
        /// <param name="plan">The plan being registered.</param>
        protected virtual void OnPlanRegistering(ExecutionPlan plan)
        {
        }

        /// <summary>
        /// The provider's on/off for a recurring handler whose plan builder didn't call <see cref="HandlerBuilder.Enabled(bool)"/>
        /// (e.g. a per-handler option, else a global one). Default: <c>true</c>.
        /// </summary>
        /// <param name="handlerType">The recurring handler type.</param>
        protected internal virtual bool IsRecurringHandlerEnabled(Type handlerType)
        {
            return true;
        }

        /// <summary>
        /// Applies the plan once the options are complete: the builder's schedule, ID and name replace the attribute's in the
        /// registrations (no per-handler option exists for them), and the plan's enabled steps are re-resolved with the
        /// provider's on/off, applying its <see cref="ExecutionPlan.MissingPrerequisite"/> again.
        /// </summary>
        internal void ResolvePlan()
        {
            if (Plan == null)
                return;

            foreach (var step in Plan.Steps)
            {
                if (!_recurringHandlers.TryGetValue(step.HandlerType, out var registration))
                    continue;

                var cron = step.Cron ?? (step.Interval.HasValue ? null : registration.Cron);
                var interval = step.Interval ?? (step.Cron != null ? null : registration.Interval);

                _recurringHandlers[step.HandlerType] = new RecurringJobRegistryInfo(
                    step.HandlerType, step.DisplayName, cron, step.Id ?? registration.Id, registration.TimeZone, interval, registration.OverlapTimeout);
            }

            Plan = Plan.Reevaluate(IsRecurringHandlerEnabled);
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
