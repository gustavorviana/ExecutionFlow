using System;

namespace ExecutionFlow.Abstractions
{
    public interface IEventDispatcher
    {
        PublishResult Publish<TEvent>(TEvent @event);
        PublishResult Schedule<TEvent>(TEvent @event, TimeSpan delay);
        PublishResult Schedule<TEvent>(TEvent @event, DateTimeOffset enqueueAt);
    }
}
