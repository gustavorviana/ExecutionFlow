using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Configuration.Handlers;

// Each handler shows one way to declare a schedule and its dependencies with attributes only.

/// <summary>Cron schedule with a stable ID and time zone; runs never overlap (a run waits up to 5 minutes).</summary>
[Recurring("*/10 * * * *", Id = "category-sync", TimeZone = "America/Sao_Paulo")]
[DisplayName("Category Sync")]
[NoOverlap(300)]
public class CategorySyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing categories");
        return Task.CompletedTask;
    }
}
