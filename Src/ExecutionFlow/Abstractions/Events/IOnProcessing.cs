namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a job starts processing.
    /// </summary>
    public interface IOnProcessing
    {
        /// <summary>Called when a job transitions to the processing state.</summary>
        /// <param name="e">The execution event details.</param>
        void OnProcessing(ExecutionEvent e);
    }
}
