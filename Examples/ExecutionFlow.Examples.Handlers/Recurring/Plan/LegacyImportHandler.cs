using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>Disabled in the plan (Enabled(false)): registered with a schedule that never fires.</summary>
[Recurring("*/5 * * * *")]
public class LegacyImportHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Warning("Legacy import ran (only through a manual trigger, since it's disabled in the plan).");
        return Task.CompletedTask;
    }
}
