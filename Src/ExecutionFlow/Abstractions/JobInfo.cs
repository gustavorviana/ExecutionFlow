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

        /// <summary>Gets the event type name as a string.</summary>
        public string EventTypeName { get; }

        /// <summary>Gets the event type, if available.</summary>
        public Type EventType { get; }

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
        {
            JobId = jobId;
            CustomId = customId;
            EventTypeName = eventTypeName;
            EventType = eventType;
            IsRecurring = isRecurring;
            State = state;
            StateChangedAt = stateChangedAt;
        }
    }
}
