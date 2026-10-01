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
        Cancelled,

        /// <summary>The job is scheduled to be enqueued later (a delayed publish, or a failed job waiting for an automatic retry).</summary>
        Scheduled,

        /// <summary>
        /// A dependent recurring job was due but didn't run, because a prerequisite in the execution plan hadn't completed a
        /// new cycle since its previous run. Listing and counting jobs in this state isn't supported yet: those calls return empty.
        /// </summary>
        PrerequisitesNotMet
    }
}
