using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal interface IFlowServiceRegistry
    {
        IFlowServiceRegistry AddSingleton<TInterface>(Type targetType);
        IFlowServiceRegistry AddSingleton<TInterface>();
        IFlowServiceRegistry AddSingleton<TInterface>(TInterface instance);
        IFlowServiceRegistry AddSingleton(Type serviceType, object instance);
        IFlowServiceRegistry AddSingleton<TInstance>(Func<TInstance> func);
        IFlowServiceRegistry RegisterLoggerFactory(IReadOnlyList<Type> loggerFactoryTypes);
    }
}
