using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionFlowRegistry
    {
        IReadOnlyDictionary<Type, HandlerRegistration> EventHandlers { get; }
        IReadOnlyList<HandlerRegistration> RecurringHandlers { get; }
    }
}
