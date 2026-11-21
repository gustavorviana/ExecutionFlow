using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Configuration.Handlers;

/// <summary>Interval with no exact cron (90 minutes): Hangfire runs it on a per-minute ticker.</summary>
[RecurringInterval("01:30:00", Id = "brand-sync")]
public class BrandSyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing brands");
        return Task.CompletedTask;
    }
}
