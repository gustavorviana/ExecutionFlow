using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionFlowRegistry
    {
        IReadOnlyList<HandlerRegistration> Registrations { get; }
    }
}
