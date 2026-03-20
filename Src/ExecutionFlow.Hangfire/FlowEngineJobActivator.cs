using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire
{
    internal class FlowEngineJobActivator : JobActivator, IServiceProvider
    {
        private readonly ConcurrentDictionary<Type, Type> _registrations = new ConcurrentDictionary<Type, Type>();
        private readonly ConcurrentDictionary<Type, object> _singletons = new ConcurrentDictionary<Type, object>();

        public FlowEngineJobActivator(IExecutionFlowRegistry registry)
        {
            AddSingleton<HangfireJobDispatcher>();

            _singletons[typeof(JobActivator)] = this;
            _singletons[typeof(IExecutionFlowRegistry)] = registry;
        }

        public void AddSingleton<TInterface>(Type targetType)
        {
            _registrations[typeof(TInterface)] = targetType;
        }

        public void AddSingleton<TInterface>()
        {
            var type = typeof(TInterface);
            _registrations[type] = type;
        }

        public void AddSingleton(Type serviceType, object instance)
        {
            _singletons[serviceType] = instance;
        }

        public void RegisterLoggerFactory(IReadOnlyList<Type> loggerFactoryTypes)
        {
            var factories = loggerFactoryTypes.Select(CreateInstance).Cast<IExecutionLoggerFactory>().ToArray();
            _singletons[typeof(ExecutionLoggerFactory)] = new ExecutionLoggerFactory(factories);
        }

        public override object ActivateJob(Type jobType)
            => GetService(jobType);

        public object GetService(Type serviceType)
        {
            if (_singletons.TryGetValue(serviceType, out var cached))
                return cached;

            if (_registrations.ContainsKey(serviceType))
                return _singletons.GetOrAdd(serviceType, x => CreateInstance(_registrations[x]));

            return CreateInstance(serviceType);
        }

        private object CreateInstance(Type type)
        {
            var ctor = type.GetConstructors().First();
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                args[i] = ActivateJob(paramType);
            }

            return ctor.Invoke(args);
        }
    }
}