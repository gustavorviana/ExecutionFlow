using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionRetryingEvent : ExecutionEvent
    {
        public int AttemptNumber { get; }

        public ExecutionRetryingEvent(string jobId, string customId, Type handlerType, int attemptNumber)
            : base(jobId, customId, handlerType)
        {
            AttemptNumber = attemptNumber;
        }
    }
}