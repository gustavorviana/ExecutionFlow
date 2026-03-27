namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Implement to be notified when a job is added to the queue.
    /// </summary>
    public interface IOnEnqueued
    {
        /// <summary>Called when a job transitions to the enqueued state.</summary>
        /// <param name="e">The execution event details.</param>
        void OnEnqueued(ExecutionEvent e);
    }
}
