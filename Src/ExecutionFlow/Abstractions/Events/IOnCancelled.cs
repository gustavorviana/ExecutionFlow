namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a job is cancelled or deleted.
    /// </summary>
    public interface IOnCancelled
    {
        /// <summary>Called when a job is cancelled.</summary>
        /// <param name="e">The execution event details.</param>
        void OnCancelled(ExecutionEvent e);
    }
}
