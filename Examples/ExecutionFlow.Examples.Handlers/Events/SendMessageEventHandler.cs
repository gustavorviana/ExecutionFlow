using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Shared.Events;

namespace ExecutionFlow.Examples.Handlers.Events;

public class SendMessageEventHandler(IEventDispatcher dispatcher) : IHandler<SendMessageEvent>
{
    public Task HandleAsync(FlowContext<SendMessageEvent> context, CancellationToken cancellationToken)
    {
        var msg = context.Event;
        context.Log.Info("Message received from '{From}' at {SentAt:HH:mm:ss}:", msg.From, msg.SentAt);
        context.Log.Success("{Content}", msg.Content);
        dispatcher.Publish(new MessageProcessedEvent { From = context.Event.From });
        return Task.CompletedTask;
    }
}
