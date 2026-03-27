namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Implement on an event to associate a custom business identifier with the job.
    /// Used for tracking, deduplication, and job management via <see cref="IExecutionManager"/>.
    /// </summary>
    public interface ICustomIdEvent
    {
        /// <summary>
        /// Gets the custom identifier for this event.
        /// </summary>
        string CustomId { get; }
    }
}
