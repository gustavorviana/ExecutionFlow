namespace ExecutionFlow.Abstractions.Events
{
    public interface IOnSucceeded
    {
        void OnSucceeded(ExecutionSucceededEvent e);
    }
}
