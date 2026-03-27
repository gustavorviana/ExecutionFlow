using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Registration metadata for an event handler.
    /// </summary>
    public class EventJobRegistryInfo : IJobRegistryInfo
    {
        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the event type this handler processes.</summary>
        public Type EventType { get; }

        /// <summary>Gets the display name for dashboard and logging.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="EventJobRegistryInfo"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="eventType">The event type.</param>
        /// <param name="displayName">The display name.</param>
        public EventJobRegistryInfo(Type handlerType, Type eventType, string displayName)
        {
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            DisplayName = displayName;
        }
    }
}
