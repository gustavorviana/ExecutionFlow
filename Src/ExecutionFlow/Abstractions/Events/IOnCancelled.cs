namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnCancelled
    {
        void OnCancelled(ExecutionEvent e);
    }
}
