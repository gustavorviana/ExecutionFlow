namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Represents the result of a <see cref="IEventDispatcher.Publish{TEvent}"/> or <see cref="IEventDispatcher.Schedule{TEvent}"/> operation.
    /// </summary>
    public class PublishResult
    {
        /// <summary>Gets the Hangfire job identifier. <c>null</c> if the job was skipped by deduplication.</summary>
        public string JobId { get; }

        /// <summary>Gets whether the job was actually enqueued. <c>false</c> if skipped by deduplication.</summary>
        public bool Enqueued { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="PublishResult"/>.
        /// </summary>
        /// <param name="jobId">The Hangfire job ID, or <c>null</c> if not enqueued.</param>
        /// <param name="enqueued">Whether the job was enqueued.</param>
        public PublishResult(string jobId, bool enqueued)
        {
            JobId = jobId;
            Enqueued = enqueued;
        }
    }
}
