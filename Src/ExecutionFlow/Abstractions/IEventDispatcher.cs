using System;

namespace ExecutionFlow.Abstractions
{
    public interface IEventDispatcher
    {
        string Publish<TEvent>(TEvent @event);
        string Schedule<TEvent>(TEvent @event, TimeSpan delay);
        string Schedule<TEvent>(TEvent @event, DateTimeOffset enqueueAt);
    }
}
