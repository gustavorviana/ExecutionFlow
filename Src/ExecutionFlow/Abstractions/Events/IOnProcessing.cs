namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnProcessing
    {
        void OnProcessing(ExecutionEvent e);
    }
}
