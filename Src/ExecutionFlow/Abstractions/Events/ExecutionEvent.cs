using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionEvent
    {
        public string JobId { get; }
        public string CustomId { get; }
        public Type HandlerType { get; }
        public TimeSpan Duration { get; }

        public ExecutionEvent(string jobId, string customId, Type handlerType, TimeSpan duration = default)
        {
            JobId = jobId;
            CustomId = customId;
            HandlerType = handlerType;
            Duration = duration;
        }
    }
}
