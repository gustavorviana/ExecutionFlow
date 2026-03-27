namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a job fails with an exception.
    /// </summary>
    public interface IOnFailed
    {
        /// <summary>Called when a job fails. Includes the exception and execution duration.</summary>
        /// <param name="e">The failed event details.</param>
        void OnFailed(ExecutionFailedEvent e);
    }
}
