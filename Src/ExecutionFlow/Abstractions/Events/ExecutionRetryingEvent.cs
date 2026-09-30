using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Event raised once per retry: when a failed attempt is scheduled to run again, or when a failed job is requeued manually.
    /// </summary>
    public class ExecutionRetryingEvent : ExecutionEvent
    {
        /// <summary>Gets the current retry attempt number (1-based).</summary>
        public int AttemptNumber { get; }

        /// <summary>
        /// Gets the exception of the attempt that failed, or <c>null</c> for a manual requeue of an already failed job.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>Gets the duration of the attempt that failed, or <see cref="TimeSpan.Zero"/> for a manual requeue.</summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionRetryingEvent"/>.
        /// </summary>
        public ExecutionRetryingEvent(string jobId, string customId, Type handlerType, int attemptNumber, Exception exception = null, TimeSpan duration = default)
            : base(jobId, customId, handlerType)
        {
            AttemptNumber = attemptNumber;
            Exception = exception;
            Duration = duration;
        }
    }
}
