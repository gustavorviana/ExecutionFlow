namespace ExecutionFlow.Abstractions
{
    public interface IDispatcher
    {
        string Enqueue<TEvent>(TEvent @event);
    }
}
