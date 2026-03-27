namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Represents the result of a <see cref="IEventDispatcher.Publish{TEvent}"/> or <see cref="IEventDispatcher.Schedule{TEvent}"/> operation.
    /// <para>
    /// <see cref="Enqueued"/> is <c>false</c> only when all of the following conditions are met:
    /// the event implements <see cref="ICustomIdEvent"/>, deduplication is enabled,
    /// and a job with the same <see cref="ICustomIdEvent.CustomId"/> is already running or pending.
    /// In all other cases, the job is always enqueued and <see cref="Enqueued"/> is <c>true</c>.
    /// </para>
    /// </summary>
    public class PublishResult
    {
        /// <summary>
        /// Gets the job identifier. Returns the custom ID if the event implements <see cref="ICustomIdEvent"/>;
        /// otherwise, returns the internal job ID. <c>null</c> when <see cref="Enqueued"/> is <c>false</c>.
        /// </summary>
        public string JobId { get; }

        /// <summary>
        /// Gets whether the job was actually enqueued.
        /// <c>false</c> only when the event implements <see cref="ICustomIdEvent"/>,
        /// deduplication is enabled, and a job with the same custom ID is already running or pending.
        /// </summary>
        public bool Enqueued { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="PublishResult"/>.
        /// </summary>
        /// <param name="jobId">The job identifier (custom ID or internal ID), or <c>null</c> if not enqueued.</param>
        /// <param name="enqueued">Whether the job was enqueued.</param>
        public PublishResult(string jobId, bool enqueued)
        {
            JobId = jobId;
            Enqueued = enqueued;
        }
    }
}
