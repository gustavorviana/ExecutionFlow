using ExecutionFlow.Abstractions;
using Hangfire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal class FlowEngineJobActivator : JobActivator, IServiceProvider
    {
        private readonly ConcurrentDictionary<Type, Type> _registrations = new ConcurrentDictionary<Type, Type>();
        private readonly ConcurrentDictionary<Type, SingletonBase> _singletons = new ConcurrentDictionary<Type, SingletonBase>();

        public FlowEngineJobActivator(IExecutionFlowRegistry registry)
        {
            AddSingleton<HangfireJobDispatcher>();

            AddSingleton<JobActivator>(this);
            AddSingleton<IServiceProvider>(this);
            AddSingleton(registry);
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

        public void AddSingleton<TInterface>(TInterface instance)
        {
            AddSingleton(typeof(TInterface), instance);
        }

        public void AddSingleton(Type serviceType, object instance)
        {
            _singletons[serviceType] = new InstanceSingletonBase(instance);
        }

        public void AddSingleton<TInstance>(Func<TInstance> func)
        {
            _singletons[typeof(TInstance)] = new FunctionSingleton<TInstance>(func);
        }

        public void RegisterLoggerFactory(IReadOnlyList<Type> loggerFactoryTypes)
        {
            var factories = loggerFactoryTypes.Select(CreateInstance).Cast<IExecutionLoggerFactory>().ToArray();
            AddSingleton(new ExecutionLoggerFactory(factories));
        }

        public override object ActivateJob(Type jobType)
            => GetService(jobType);

        public object GetService(Type serviceType)
        {
            if (_singletons.TryGetValue(serviceType, out var cached))
                return cached.GetInstance();

            if (_registrations.ContainsKey(serviceType))
                return _singletons
                    .GetOrAdd(serviceType, x => new InstanceSingletonBase(CreateInstance(_registrations[x])))
                    .GetInstance();

            return CreateInstance(serviceType);
        }

        private object CreateInstance(Type type)
        {
            if (type.IsInterface)
                throw new InvalidOperationException(
                    $"The interface '{type.FullName}' cannot be instantiated. Register it using AddSingleton or another DI method."
                );

            if (type.IsAbstract)
                throw new InvalidOperationException(
                    $"The abstract type '{type.FullName}' cannot be instantiated. Register a concrete implementation using AddSingleton or another DI method."
                );

            var ctors = type.GetConstructors();
            if (ctors.Length == 0)
                throw new InvalidOperationException($"Type '{type.FullName}' has no public constructors and cannot be instantiated.");

            var ctor = ctors[0];
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


        private abstract class SingletonBase
        {
            public abstract object GetInstance();
        }

        private class FunctionSingleton<TInstance> : SingletonBase
        {
            private readonly object _lock = new object();
            private readonly Func<TInstance> _call;
            private TInstance _instance;

            public FunctionSingleton(Func<TInstance> call)
            {
                _call = call;
            }

            public override object GetInstance()
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = _call();

                    return _instance;
                }
            }
        }

        private class InstanceSingletonBase : SingletonBase
        {
            private readonly object _instance;

            public InstanceSingletonBase(object instance)
            {
                _instance = instance;
            }

            public override object GetInstance() => _instance;
        }
    }
}