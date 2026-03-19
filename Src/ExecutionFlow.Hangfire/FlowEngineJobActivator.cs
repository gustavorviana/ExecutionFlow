using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire
{
    internal class FlowEngineJobActivator : JobActivator, IServiceProvider
    {
        private readonly Dictionary<Type, Type> _registrations = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

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

        public override object ActivateJob(Type jobType)
            => GetService(jobType);

        public object GetService(Type serviceType)
        {
            if (_singletons.TryGetValue(serviceType, out var cached))
                return cached;

            if (_registrations.TryGetValue(serviceType, out var implType))
            {
                var instance = CreateInstance(implType);
                _singletons[serviceType] = instance;
                return instance;
            }

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