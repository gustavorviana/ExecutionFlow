using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// Root of the plan that fails now and then: a failed run doesn't count as a cycle, so its dependents aren't triggered.
/// </summary>
[Recurring("*/3 * * * *")]
[DisplayName("Plan: Brand Sync")]
public class BrandSyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing brands...");
        await Task.Delay(1500, cancellationToken);

        if (Random.Shared.Next(4) == 0)
            throw new InvalidOperationException("Simulated failure: the brands source is unavailable. Dependents keep waiting.");

        context.Log.Success("Brands synced.");
    }
}
