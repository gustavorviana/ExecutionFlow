using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Shared.Events;

namespace ExecutionFlow.Examples.Handlers;

public class SendMessageHandler : IHandler<SendMessageEvent>
{
    public Task HandleAsync(FlowContext<SendMessageEvent> context, CancellationToken cancellationToken)
    {
        var msg = context.Event;
        context.Log.Info($"Message received from '{msg.From}' at {msg.SentAt:HH:mm:ss}:");
        context.Log.Success(msg.Content);
        return Task.CompletedTask;
    }
}
