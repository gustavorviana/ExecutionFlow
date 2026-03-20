namespace ExecutionFlow.Abstractions
{
    public interface IEventDispatcher
    {
        string Publish<TEvent>(TEvent @event);
    }
}
