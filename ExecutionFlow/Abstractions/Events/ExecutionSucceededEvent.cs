using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionSucceededEvent : ExecutionEvent
    {
        public TimeSpan Duration { get; }

        public ExecutionSucceededEvent(string jobId, string displayName, string customId, Type handlerType, TimeSpan duration)
            : base(jobId, displayName, customId, handlerType)
        {
            Duration = duration;
        }
    }
}
