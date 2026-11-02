using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Shared.Events;

namespace ExecutionFlow.Examples.Handlers.Events;

/// <summary>Processes an order; long enough to try IsRunning, Cancel and the deduplication from the Consumer's API.</summary>
public class OrderCreatedEventHandler : IHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(FlowContext<OrderCreatedEvent> context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Processing order {context.Event.OrderId} (attempt {context.AttemptNumber})");
        await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
        context.Log.Info($"Order {context.Event.OrderId} processed");
    }
}
