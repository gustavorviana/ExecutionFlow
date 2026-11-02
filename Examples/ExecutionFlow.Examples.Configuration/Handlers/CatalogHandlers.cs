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

/// <summary>Interval with no exact cron (90 minutes): Hangfire runs it on a per-minute ticker.</summary>
[RecurringInterval("01:30:00", Id = "brand-sync")]
public class BrandSyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info("Syncing brands");
        return Task.CompletedTask;
    }
}

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
