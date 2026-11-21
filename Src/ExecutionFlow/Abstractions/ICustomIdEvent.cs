namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Implement on an event to associate a custom business identifier with the job.
    /// Used for tracking, deduplication, and job management via <see cref="IExecutionManager"/>.
    /// </summary>
    public interface ICustomIdEvent
    {
        /// <summary>
        /// Gets the custom identifier for this event. <c>null</c> or empty means no custom ID. Otherwise it can only contain
        /// letters, digits, spaces and <c>_ - . + ` [ ] , =</c>, that doesn't start or end with a space; publishing or
        /// scheduling an event with a custom ID outside this format throws <see cref="System.ArgumentException"/>.
        /// </summary>
        string CustomId { get; }
    }
}
