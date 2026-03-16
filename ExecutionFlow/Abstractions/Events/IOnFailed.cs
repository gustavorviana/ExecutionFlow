namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnFailed
    {
        void OnFailed(ExecutionFailedEvent e);
    }
}
