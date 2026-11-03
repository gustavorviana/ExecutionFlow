using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Configuration.Handlers;

/// <summary>Interval with an exact cron (15 minutes): Hangfire registers "*/15 * * * *" in UTC.</summary>
[RecurringInterval("00:15:00")]
public class PriceAuditHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Auditing prices");
        return Task.CompletedTask;
    }
}
