namespace ExecutionFlow.Abstractions
{
    public interface IDispatcher
    {
        string Publish<TEvent>(TEvent @event);
    }
}
