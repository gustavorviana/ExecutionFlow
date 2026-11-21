using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Contains metadata about a background job.
    /// </summary>
    public class JobInfo
    {
        /// <summary>Gets the background job identifier.</summary>
        public string JobId { get; }

        /// <summary>Gets the custom business identifier, if set via <see cref="ICustomIdEvent"/>.</summary>
        public string CustomId { get; }

        /// <summary>Gets the event type name for event jobs, or <c>null</c> for recurring jobs and jobs whose type can't be loaded.</summary>
        public string EventTypeName { get; }

        /// <summary>Gets the event type for event jobs, or <c>null</c> for recurring jobs and jobs whose type can't be loaded.</summary>
        public Type EventType { get; }

        /// <summary>Gets the handler type for recurring jobs, or <c>null</c> for event jobs.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets whether this job is a recurring service execution.</summary>
        public bool IsRecurring { get; }

        /// <summary>Gets the current state of the job.</summary>
        public JobState State { get; }

        /// <summary>Gets the timestamp of the last state change.</summary>
        public DateTimeOffset? StateChangedAt { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="JobInfo"/>.
        /// </summary>
        public JobInfo(string jobId, string customId, string eventTypeName, Type eventType, bool isRecurring, JobState state, DateTimeOffset? stateChangedAt)
            : this(jobId, customId, eventTypeName, eventType, null, isRecurring, state, stateChangedAt)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JobInfo"/>.
        /// </summary>
        public JobInfo(string jobId, string customId, string eventTypeName, Type eventType, Type handlerType, bool isRecurring, JobState state, DateTimeOffset? stateChangedAt)
        {
            JobId = jobId;
            CustomId = customId;
            EventTypeName = eventTypeName;
            EventType = eventType;
            HandlerType = handlerType;
            IsRecurring = isRecurring;
            State = state;
            StateChangedAt = stateChangedAt;
        }
    }
}
