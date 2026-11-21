using ExecutionFlow.Abstractions;
using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Sets the deduplication behavior for an event type, overriding the global default.
    /// Apply to event classes implementing <see cref="ICustomIdEvent"/>; it has no effect on events without a custom ID.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DeduplicationAttribute : Attribute
    {
        /// <summary>Gets the deduplication behavior for this event type.</summary>
        public DeduplicationBehavior Behavior { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="DeduplicationAttribute"/>.
        /// </summary>
        /// <param name="behavior">The deduplication behavior for this event type.</param>
        public DeduplicationAttribute(DeduplicationBehavior behavior)
        {
            Behavior = behavior;
        }
    }
}
