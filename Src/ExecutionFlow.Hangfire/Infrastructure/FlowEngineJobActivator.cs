using ExecutionFlow.Abstractions;
using Hangfire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// The built-in activator for apps without Microsoft DI: an internal fallback that activates ExecutionFlow's jobs and
    /// their dependencies, not the app's DI. Registered types are singletons, created once even when resolved concurrently;
    /// other concrete types are transient. A constructor cycle throws instead of overflowing the stack.
    /// </summary>
    internal class FlowEngineJobActivator : JobActivator, IServiceProvider, IFlowServiceRegistry
    {
        private readonly ConcurrentDictionary<Type, Type> _registrations = new ConcurrentDictionary<Type, Type>();
        private readonly ConcurrentDictionary<Type, SingletonBase> _singletons = new ConcurrentDictionary<Type, SingletonBase>();
        private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructors = new ConcurrentDictionary<Type, ConstructorInfo>();

        // Types whose constructor graph was checked for cycles; cleared when a registration changes the graph.
        private readonly ConcurrentDictionary<Type, bool> _acyclic = new ConcurrentDictionary<Type, bool>();

        public FlowEngineJobActivator(IExecutionFlowRegistry registry)
        {
            AddSingleton<HangfireJobDispatcher>();

            AddSingleton<JobActivator>(this);
            AddSingleton<IServiceProvider>(this);
            AddSingleton(registry);
        }

        public IFlowServiceRegistry AddSingleton<TInterface>(Type targetType)
        {
            _registrations[typeof(TInterface)] = targetType;
            _acyclic.Clear();
            return this;
        }

        public IFlowServiceRegistry AddSingleton<TInterface>()
        {
            var type = typeof(TInterface);
            _registrations[type] = type;
            _acyclic.Clear();
            return this;
        }

        public IFlowServiceRegistry AddSingleton<TInterface>(TInterface instance)
        {
            AddSingleton(typeof(TInterface), instance);
            return this;
        }

        public IFlowServiceRegistry AddSingleton(Type serviceType, object instance)
        {
            _singletons[serviceType] = new InstanceSingletonBase(instance);
            _acyclic.Clear();
            return this;
        }

        public IFlowServiceRegistry AddSingleton<TInstance>(Func<TInstance> func)
        {
            _singletons[typeof(TInstance)] = new FunctionSingleton<TInstance>(func);
            _acyclic.Clear();
            return this;
        }

        public IFlowServiceRegistry RegisterLoggerFactory(IReadOnlyList<Type> loggerFactoryTypes)
        {
            var factories = loggerFactoryTypes.Select(CreateInstance).Cast<IExecutionLoggerFactory>().ToArray();
            AddSingleton(new ExecutionLoggerFactory(factories));
            return this;
        }

        public override object ActivateJob(Type jobType)
            => GetService(jobType);

        public object GetService(Type serviceType)
        {
            if (_singletons.TryGetValue(serviceType, out var cached))
                return cached.GetInstance();

            if (_registrations.TryGetValue(serviceType, out var targetType))
            {
                EnsureNoCycle(serviceType);

                // GetOrAdd may build two wrappers under concurrency, but only the stored one ever creates the instance.
                return _singletons
                    .GetOrAdd(serviceType, _ => new FunctionSingleton<object>(() => CreateInstance(targetType)))
                    .GetInstance();
            }

            EnsureNoCycle(serviceType);
            return CreateInstance(serviceType);
        }

        private object CreateInstance(Type type)
        {
            var ctor = GetConstructor(type);
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

        private ConstructorInfo GetConstructor(Type type)
        {
            if (_constructors.TryGetValue(type, out var cached))
                return cached;

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

            if (ctors.Length > 1)
                throw new InvalidOperationException($"Type '{type.FullName}' has multiple public constructors. Only types with a single public constructor are supported.");

            return _constructors.GetOrAdd(type, ctors[0]);
        }

        /// <summary>
        /// Walks the constructor graph from <paramref name="serviceType"/> before anything is created, and throws when a type
        /// needs itself (directly or through others): the recursion would otherwise overflow the stack and end the process.
        /// Types the walk can't build (interfaces, abstract types, several constructors) are left to the activation, which
        /// throws its usual error.
        /// </summary>
        private void EnsureNoCycle(Type serviceType)
        {
            if (_acyclic.ContainsKey(serviceType))
                return;

            Visit(serviceType, new List<Type>());
            _acyclic.TryAdd(serviceType, true);
        }

        private void Visit(Type serviceType, List<Type> chain)
        {
            // Instances and factories are opaque: nothing is constructed through them.
            if (_singletons.ContainsKey(serviceType) && !_registrations.ContainsKey(serviceType))
                return;

            if (chain.Contains(serviceType))
            {
                chain.Add(serviceType);
                throw new InvalidOperationException(
                    "The built-in activator found a constructor cycle: " + string.Join(" -> ", chain.Select(t => t.FullName)) +
                    ". Break the cycle, or use Microsoft DI (AddHangfireToExecutionFlow).");
            }

            var implementation = _registrations.TryGetValue(serviceType, out var target) ? target : serviceType;
            if (implementation.IsInterface || implementation.IsAbstract)
                return;

            var ctors = implementation.GetConstructors();
            if (ctors.Length != 1)
                return;

            chain.Add(serviceType);
            foreach (var parameter in ctors[0].GetParameters())
                Visit(parameter.ParameterType, chain);
            chain.RemoveAt(chain.Count - 1);
        }


        private abstract class SingletonBase
        {
            public abstract object GetInstance();
        }

        /// <summary>
        /// Creates the instance on first use, once even under concurrency. A creation that throws (or returns <c>null</c>)
        /// stores nothing, so the next resolution tries again.
        /// </summary>
        private class FunctionSingleton<TInstance> : SingletonBase
        {
            private readonly object _lock = new object();
            private readonly Func<TInstance> _call;
            private volatile object _instance;

            public FunctionSingleton(Func<TInstance> call)
            {
                _call = call;
            }

            public override object GetInstance()
            {
                var instance = _instance;
                if (instance != null)
                    return instance;

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