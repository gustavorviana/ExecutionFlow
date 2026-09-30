using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Base event raised during job lifecycle transitions. Contains job metadata.
    /// Events that relate to a finished attempt (<see cref="ExecutionSucceededEvent"/>, <see cref="ExecutionFailedEvent"/>,
    /// <see cref="ExecutionRetryingEvent"/>) also carry the attempt's duration.
    /// </summary>
    public class ExecutionEvent
    {
        /// <summary>Gets the background job identifier.</summary>
        public string JobId { get; }

        /// <summary>Gets the custom business identifier, if set.</summary>
        public string CustomId { get; }

        /// <summary>Gets the handler type that processes the job, or <c>null</c> when it isn't registered on this host.</summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionEvent"/>.
        /// </summary>
        public ExecutionEvent(string jobId, string customId, Type handlerType)
        {
            JobId = jobId;
            CustomId = customId;
            HandlerType = handlerType;
        }
    }
}
