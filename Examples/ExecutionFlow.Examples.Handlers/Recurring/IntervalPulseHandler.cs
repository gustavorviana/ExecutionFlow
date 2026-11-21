using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Handlers.Recurring;

/// <summary>
/// Every 30 seconds: an interval with seconds that divides a minute, registered in Hangfire as "*/30 * * * * *". Hangfire
/// runs it at most once per SchedulePollingInterval (15 s by default), so its precision is a few seconds.
/// </summary>
[RecurringInterval("00:00:30")]
[DisplayName("Interval pulse (30 s)")]
public class IntervalPulseHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Pulse at {DateTime.UtcNow:HH:mm:ss} UTC");
        return Task.CompletedTask;
    }
}
