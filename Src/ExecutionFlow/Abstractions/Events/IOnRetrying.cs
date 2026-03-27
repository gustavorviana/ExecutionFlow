namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a failed job is being retried.
    /// </summary>
    public interface IOnRetrying
    {
        /// <summary>Called when a job is scheduled for retry. Includes attempt number and duration.</summary>
        /// <param name="e">The retrying event details.</param>
        void OnRetrying(ExecutionRetryingEvent e);
    }
}
