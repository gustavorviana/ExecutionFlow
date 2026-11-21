using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Handlers.Recurring;

/// <summary>
/// Every 15 minutes with an interval instead of a cron. On Hangfire, prefer [Recurring]; 15 minutes has an exact cron, so it's
/// registered as "*/15 * * * *" anyway. Runs never overlap: a run waits up to 10 minutes for a running one.
/// </summary>
[RecurringInterval("00:15:00", Id = "interval-price-sync")]
[NoOverlap(600)]
[DisplayName("Interval: Price Sync")]
public class IntervalPriceSyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Price sync (every 15 min) at {DateTime.UtcNow:HH:mm:ss} UTC");
        return Task.CompletedTask;
    }
}
