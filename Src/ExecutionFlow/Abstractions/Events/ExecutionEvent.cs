using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Base event raised during job lifecycle transitions. Contains job metadata and execution duration.
    /// </summary>
    public class ExecutionEvent
    {
        /// <summary>Gets the background job identifier.</summary>
        public string JobId { get; }

        /// <summary>Gets the custom business identifier, if set.</summary>
        public string CustomId { get; }

        /// <summary>Gets the handler type that processed the job.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the execution duration since processing started.</summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionEvent"/>.
        /// </summary>
        public ExecutionEvent(string jobId, string customId, Type handlerType, TimeSpan duration = default)
        {
            JobId = jobId;
            CustomId = customId;
            HandlerType = handlerType;
            Duration = duration;
        }
    }
}
