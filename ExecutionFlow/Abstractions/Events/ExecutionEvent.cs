using System;

namespace ExecutionFlow.Abstractions.Events
{
    public class ExecutionEvent
    {
        public string JobId { get; }
        public string DisplayName { get; }
        public string CustomId { get; }
        public Type HandlerType { get; }

        public ExecutionEvent(string jobId, string displayName, string customId, Type handlerType)
        {
            JobId = jobId;
            DisplayName = displayName;
            CustomId = customId;
            HandlerType = handlerType;
        }
    }
}
