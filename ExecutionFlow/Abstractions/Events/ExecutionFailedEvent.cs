using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionFailedEvent : ExecutionEvent
    {
        public Exception Exception { get; }

        public ExecutionFailedEvent(string jobId, string displayName, string customId, Type handlerType, Exception exception)
            : base(jobId, displayName, customId, handlerType)
        {
            Exception = exception;
        }
    }
}
