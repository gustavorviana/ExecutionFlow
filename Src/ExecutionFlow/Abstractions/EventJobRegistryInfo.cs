using System;

namespace ExecutionFlow.Abstractions
{
    public class EventJobRegistryInfo : IJobRegistryInfo
    {
        public Type HandlerType { get; }
        public Type EventType { get; }
        public string DisplayName { get; }

        public EventJobRegistryInfo(Type handlerType, Type eventType, string displayName)
        {
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            DisplayName = displayName;
        }
    }
}
