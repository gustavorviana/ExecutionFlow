using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionFlowRegistry
    {
        IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers { get; }
        IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers { get; }
    }
}
