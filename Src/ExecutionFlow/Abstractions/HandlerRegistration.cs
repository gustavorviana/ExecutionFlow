using System;

namespace ExecutionFlow.Abstractions
{
    public class HandlerRegistration
    {
        public Type HandlerType { get; }
        public Type EventType { get; }
        public bool IsRecurring { get; }
        public string DisplayName { get; }
        public string Cron { get; }

        public HandlerRegistration(Type handlerType, Type eventType, string displayName, string cron)
        {
            HandlerType = handlerType;
            EventType = eventType;
            IsRecurring = eventType == null;
            DisplayName = displayName;
            Cron = cron;
        }
    }
}
