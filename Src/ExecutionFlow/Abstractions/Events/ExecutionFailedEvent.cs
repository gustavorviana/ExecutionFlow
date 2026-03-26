using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionFailedEvent : ExecutionEvent
    {
        public Exception Exception { get; }

        public ExecutionFailedEvent(string jobId, string customId, Type handlerType, Exception exception, TimeSpan duration = default)
            : base(jobId, customId, handlerType, duration)
        {
            Exception = exception;
        }
    }
}
