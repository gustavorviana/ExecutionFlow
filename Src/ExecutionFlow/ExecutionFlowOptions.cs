using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExecutionFlow
{
    public abstract class ExecutionFlowOptions
    {
        private bool _locked;
        public Action<AssemblyTypeScanContext> OnTypeLoadFailure { get; set; }


        private readonly List<Type> _loggerFactoryTypes = new List<Type>();
        private readonly Dictionary<Type, RecurringJobRegistryInfo> _recurringHandlers = new Dictionary<Type, RecurringJobRegistryInfo>(new TypeEqualityComparer());
        private readonly Dictionary<Type, EventJobRegistryInfo> _eventHandlers = new Dictionary<Type, EventJobRegistryInfo>(new TypeEqualityComparer());

        public IReadOnlyList<Type> LoggerFactoryTypes => _loggerFactoryTypes;
        public IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers => _recurringHandlers;
        public IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers => _eventHandlers;


        public void Scan(Assembly assembly)
        {
            ThrowIfLocked();

            foreach (var type in GetValidTypes(assembly))
                if (!type.IsAbstract && !type.IsInterface)
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

        public void Add(Type handlerType)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            var recurringAttr = handlerType.GetCustomAttribute<Attributes.RecurringAttribute>();
            var displayNameAttr = handlerType.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
            var displayName = displayNameAttr?.DisplayName ?? handlerType.Name;
            var cron = recurringAttr?.Cron;

            // Check for IHandler (non-generic)
            if (typeof(IHandler).IsAssignableFrom(handlerType))
            {
                _recurringHandlers[handlerType] = new RecurringJobRegistryInfo(
                    handlerType: handlerType,
                    displayName: displayName,
                    cron: cron
                );
                return;
            }

            // Check for IHandler<TEvent>
            foreach (var iface in handlerType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandler<>))
                {
                    var eventType = iface.GetGenericArguments()[0];

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
            }
        }

        public void AddLogger<TFactory>() where TFactory : class, IExecutionLoggerFactory
        {
            AddLogger(typeof(TFactory));
        }

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

        protected void ThrowIfLocked()
        {
            if (_locked)
                throw new InvalidOperationException("Options cannot be modified after Configure has completed.");
        }
    }
}
