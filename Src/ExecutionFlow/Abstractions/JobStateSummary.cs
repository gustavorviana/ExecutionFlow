namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Contains the job count for each <see cref="JobState"/>.
    /// </summary>
    public class JobStateSummary
    {
        /// <summary>Gets the number of jobs waiting in the queue.</summary>
        public long Enqueued { get; }

        /// <summary>Gets the number of jobs currently being processed.</summary>
        public long Processing { get; }

        /// <summary>Gets the number of jobs that completed successfully.</summary>
        public long Succeeded { get; }

        /// <summary>Gets the number of jobs that failed during execution.</summary>
        public long Failed { get; }

        /// <summary>Gets the number of jobs that were cancelled or deleted.</summary>
        public long Cancelled { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="JobStateSummary"/>.
        /// </summary>
        public JobStateSummary(long enqueued, long processing, long succeeded, long failed, long cancelled)
        {
            Enqueued = enqueued;
            Processing = processing;
            Succeeded = succeeded;
            Failed = failed;
            Cancelled = cancelled;
        }
    }
}
