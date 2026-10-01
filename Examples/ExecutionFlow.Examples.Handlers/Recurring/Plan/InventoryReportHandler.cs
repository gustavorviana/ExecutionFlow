using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// RunOnOwnSchedule: runs on its own cron, but only when products completed a new cycle since its last run. Otherwise the
/// occurrence ends in the PrerequisitesNotMet state (see it in the dashboard).
/// </summary>
[Recurring("* * * * *")]
[DisplayName("Plan: Inventory Report")]
public class InventoryReportHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Success("Inventory report generated from the latest products.");
        return Task.CompletedTask;
    }
}
