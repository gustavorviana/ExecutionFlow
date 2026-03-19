using System;

namespace ExecutionFlow.Abstractions
{
    public class JobInfo
    {
        public string JobId { get; }
        public string CustomId { get; }
        public string EventTypeName { get; }
        public Type EventType { get; }
        public JobState State { get; }
        public DateTimeOffset? StateChangedAt { get; }

        public JobInfo(string jobId, string customId, string eventTypeName, Type eventType, JobState state, DateTimeOffset? stateChangedAt)
        {
            JobId = jobId;
            CustomId = customId;
            EventTypeName = eventTypeName;
            EventType = eventType;
            State = state;
            StateChangedAt = stateChangedAt;
        }
    }
}
