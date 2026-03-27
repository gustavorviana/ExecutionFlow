using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
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
        public Action<AssemblyTypeScanContext> OnTypeLoadFailure { get; set; }

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
                _recurringHandlers[handlerType] = new RecurringJobRegistryInfo(
                    handlerType: handlerType,
                    displayName: displayName,
                    cron: cron
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
