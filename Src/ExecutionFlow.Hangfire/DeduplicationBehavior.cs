namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Defines how duplicate events with the same CustomId are handled when publishing.
    /// </summary>
    public enum DeduplicationBehavior
    {
        /// <summary>No deduplication. Always enqueues the job.</summary>
        Disabled,
        /// <summary>Skips enqueuing if a job with the same CustomId is already running or pending.</summary>
        SkipIfExists,
        /// <summary>Cancels the existing job and enqueues a new one.</summary>
        ReplaceExisting
    }
}
