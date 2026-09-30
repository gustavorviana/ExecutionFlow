using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Event raised when a job fails for good: its last attempt failed and no retry follows.
    /// </summary>
    public class ExecutionFailedEvent : ExecutionEvent
    {
        /// <summary>Gets the exception that caused the failure.</summary>
        public Exception Exception { get; }

        /// <summary>Gets the duration of the failed attempt.</summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionFailedEvent"/>.
        /// </summary>
        public ExecutionFailedEvent(string jobId, string customId, Type handlerType, Exception exception, TimeSpan duration = default)
            : base(jobId, customId, handlerType)
        {
            Exception = exception;
            Duration = duration;
        }
    }
}
