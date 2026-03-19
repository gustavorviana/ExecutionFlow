using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionSucceededEvent : ExecutionEvent
    {
        public TimeSpan Duration { get; }

        public ExecutionSucceededEvent(string jobId, string customId, Type handlerType, TimeSpan duration)
            : base(jobId, customId, handlerType)
        {
            Duration = duration;
        }
    }
}
