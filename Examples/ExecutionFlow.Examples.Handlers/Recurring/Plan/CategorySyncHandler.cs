using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>Root of the plan: runs on its own cron and triggers what depends on it.</summary>
[Recurring("*/2 * * * *")]
[DisplayName("Plan: Category Sync")]
public class CategorySyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing categories...");
        await Task.Delay(2000, cancellationToken);
        context.Log.Success("Categories synced. Dependents whose prerequisites are all ready get triggered now.");
    }
}
