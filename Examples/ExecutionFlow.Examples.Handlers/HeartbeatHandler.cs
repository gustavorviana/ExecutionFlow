using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers;

[Recurring("* * * * *")]
[DisplayName("Heartbeat")]
public class HeartbeatHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Heartbeat at {DateTime.UtcNow:HH:mm:ss} UTC");
        return Task.CompletedTask;
    }
}
