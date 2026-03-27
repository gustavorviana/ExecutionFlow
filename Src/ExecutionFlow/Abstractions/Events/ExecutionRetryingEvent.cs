using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Event raised when a failed job is being retried.
    /// </summary>
    public class ExecutionRetryingEvent : ExecutionEvent
    {
        /// <summary>Gets the current retry attempt number (1-based).</summary>
        public int AttemptNumber { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionRetryingEvent"/>.
        /// </summary>
        public ExecutionRetryingEvent(string jobId, string customId, Type handlerType, int attemptNumber, TimeSpan duration = default)
            : base(jobId, customId, handlerType, duration)
        {
            AttemptNumber = attemptNumber;
        }
    }
}
