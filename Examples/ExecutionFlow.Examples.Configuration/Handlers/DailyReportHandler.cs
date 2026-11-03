using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Configuration.Handlers;

/// <summary>A dependent on its own schedule: every day at 8:00, but only when products completed a new cycle.</summary>
[Recurring("0 8 * * *")]
[DependsOn(typeof(ProductSyncHandler))]
[DependentRun(DependentRunMode.OwnSchedule)]
public class DailyReportHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Building the daily report");
        return Task.CompletedTask;
    }
}
