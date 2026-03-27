namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a job completes successfully.
    /// </summary>
    public interface IOnSucceeded
    {
        /// <summary>Called when a job succeeds. Includes execution duration.</summary>
        /// <param name="e">The succeeded event details.</param>
        void OnSucceeded(ExecutionSucceededEvent e);
    }
}
