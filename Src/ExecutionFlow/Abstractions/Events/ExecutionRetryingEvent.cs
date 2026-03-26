using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionRetryingEvent : ExecutionEvent
    {
        public int AttemptNumber { get; }

        public ExecutionRetryingEvent(string jobId, string customId, Type handlerType, int attemptNumber, TimeSpan duration = default)
            : base(jobId, customId, handlerType, duration)
        {
            AttemptNumber = attemptNumber;
        }
    }
}