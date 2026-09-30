namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Defines how an event is handled when an active job with the same <see cref="ICustomIdEvent.CustomId"/> exists.
    /// A job is active while it is enqueued, scheduled (including waiting for a retry), awaiting, or processing.
    /// </summary>
    public enum DeduplicationBehavior
    {
        /// <summary>No deduplication. Always creates the job.</summary>
        Disabled,
        /// <summary>Skips the event if an active job with the same custom ID exists.</summary>
        SkipIfExists,
        /// <summary>Deletes the active job with the same custom ID and creates a new one.</summary>
        ReplaceExisting
    }
}
