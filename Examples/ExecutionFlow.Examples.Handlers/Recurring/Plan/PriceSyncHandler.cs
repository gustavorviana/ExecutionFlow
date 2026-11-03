using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>Dependency declared by attribute: runs right after each completed product sync.</summary>
[DependsOn(typeof(ProductSyncHandler))]
[DisplayName("Plan: Price Sync")]
public class PriceSyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing prices...");
        await Task.Delay(1000, cancellationToken);
        context.Log.Success("Prices synced.");
    }
}
