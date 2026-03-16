using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionRetryingEvent : ExecutionEvent
    {
        public int AttemptNumber { get; }

        public ExecutionRetryingEvent(string jobId, string displayName, string customId, Type handlerType, int attemptNumber)
            : base(jobId, displayName, customId, handlerType)
        {
            AttemptNumber = attemptNumber;
        }
    }
}
