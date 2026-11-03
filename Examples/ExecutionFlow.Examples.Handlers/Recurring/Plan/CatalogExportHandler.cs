using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// Depends on prices and, directly, on categories (Category → Product → Price → Export, plus Category → Export).
/// Categories alone don't trigger it: it runs once prices also have a new cycle.
/// </summary>
public class CatalogExportHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Exporting the catalog...");
        await Task.Delay(1000, cancellationToken);
        context.Log.Success("Catalog exported.");
    }
}
