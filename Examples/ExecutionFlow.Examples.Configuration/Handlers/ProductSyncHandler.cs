using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Configuration.Handlers;

/// <summary>
/// A dependent declared by attribute: it waits for a new cycle of both prerequisites, and never runs less than 20 minutes
/// after its previous run. Without a user plan these attributes form the automatic plan.
/// </summary>
[DependsOn(typeof(CategorySyncHandler))]
[DependsOn(typeof(BrandSyncHandler))]
[DependentRun(DependentRunMode.MinInterval, "00:20:00")]
public class ProductSyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing products");
        return Task.CompletedTask;
    }
}
