using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Handlers.Recurring;

/// <summary>
/// Every 7 minutes: no cron can express it, so on Hangfire it runs on a per-minute recurring job that skips each minute
/// until 7 minutes have passed since the last run. Skipped minutes create no job.
/// </summary>
[RecurringInterval("00:07:00")]
[DisplayName("Interval: Stock Audit")]
public class IntervalStockAuditHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Stock audit (every 7 min) at {DateTime.UtcNow:HH:mm:ss} UTC");
        return Task.CompletedTask;
    }
}
