namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Represents the lifecycle state of a background job.
    /// </summary>
    public enum JobState
    {
        /// <summary>The job is waiting in the queue to be processed.</summary>
        Enqueued,
        /// <summary>The job is currently being processed.</summary>
        Processing,
        /// <summary>The job completed successfully.</summary>
        Succeeded,
        /// <summary>The job failed during execution.</summary>
        Failed,
        /// <summary>The job was cancelled or deleted.</summary>
        Cancelled
    }
}
