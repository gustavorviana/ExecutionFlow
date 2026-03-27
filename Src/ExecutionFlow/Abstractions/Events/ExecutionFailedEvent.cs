using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Event raised when a job fails during execution.
    /// </summary>
    public class ExecutionFailedEvent : ExecutionEvent
    {
        /// <summary>Gets the exception that caused the failure.</summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionFailedEvent"/>.
        /// </summary>
        public ExecutionFailedEvent(string jobId, string customId, Type handlerType, Exception exception, TimeSpan duration = default)
            : base(jobId, customId, handlerType, duration)
        {
            Exception = exception;
        }
    }
}
