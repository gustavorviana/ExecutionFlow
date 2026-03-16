namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnEnqueued
    {
        void OnEnqueued(ExecutionEvent e);
    }
}
