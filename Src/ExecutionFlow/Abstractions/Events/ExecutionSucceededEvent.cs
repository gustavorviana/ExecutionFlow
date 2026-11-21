using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Event raised when a job completes successfully.
    /// </summary>
    public class ExecutionSucceededEvent : ExecutionEvent
    {
        /// <summary>Gets the duration of the successful attempt.</summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionSucceededEvent"/>.
        /// </summary>
        public ExecutionSucceededEvent(string jobId, string customId, Type handlerType, TimeSpan duration)
            : base(jobId, customId, handlerType)
        {
            Duration = duration;
        }
    }
}
