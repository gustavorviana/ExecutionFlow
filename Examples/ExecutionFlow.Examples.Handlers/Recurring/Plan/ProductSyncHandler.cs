using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// Depends on categories and brands (the last one to complete triggers it) and runs at least 5 minutes after its previous
/// run finished (MinInterval). No [Recurring]: its prerequisites trigger it.
/// </summary>
public class ProductSyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing products (new cycles of categories and brands are available)...");
        await Task.Delay(3000, cancellationToken);
        context.Log.Success("Products synced.");
    }
}
