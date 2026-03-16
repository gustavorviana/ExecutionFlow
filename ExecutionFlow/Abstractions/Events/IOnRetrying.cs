namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnRetrying
    {
        void OnRetrying(ExecutionRetryingEvent e);
    }
}
