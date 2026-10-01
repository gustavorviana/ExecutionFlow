using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// The example's execution plan: which recurring handlers wait for which, and how each dependent runs.
/// </summary>
/// <remarks>
/// <code>
///   CategorySync (*/2) ──┬──► ProductSync (min 5 min) ──► PriceSync ([DependsOn]) ──► CatalogExport
///   BrandSync (*/3, fails ~25%) ┘        │                                              ▲
///                                        └──► InventoryReport (own cron, * * * * *)     │
///   CategorySync ───────────────────────────────────────────────────────────────────────┘
///
///   LegacyImport (disabled) ──► LegacyCleanup (disabled in cascade)
/// </code>
/// </remarks>
public static class CatalogPlan
{
    public static ExecutionPlan Build()
    {
        var planner = new ExecutionPlanner();

        // Roots: they run on their own [Recurring] cron.
        planner.Add<CategorySyncHandler>();
        planner.Add<BrandSyncHandler>();

        // Default mode: triggered when its prerequisites are ready. Two prerequisites: the last one to complete triggers it.
        // MinInterval: never less than 5 minutes after its previous run finished; an early trigger is postponed, not lost.
        planner.Add<ProductSyncHandler>()
            .DependsOn<CategorySyncHandler>()
            .DependsOn<BrandSyncHandler>()
            .MinInterval(TimeSpan.FromMinutes(5))
            .DisplayName("Plan: Product Sync");

        // The prerequisite comes from [DependsOn(typeof(ProductSyncHandler))] on the class.
        planner.Add<PriceSyncHandler>();

        // Category → Product → Price → Export, plus Category → Export: categories alone don't trigger it.
        planner.Add<CatalogExportHandler>()
            .DependsOn<PriceSyncHandler>()
            .DependsOn<CategorySyncHandler>()
            .DisplayName("Plan: Catalog Export");

        // Keeps its own cron; each occurrence without a new product cycle ends in PrerequisitesNotMet.
        planner.Add<InventoryReportHandler>()
            .DependsOn<ProductSyncHandler>()
            .RunOnOwnSchedule();

        // Disabled: registered with a schedule that never fires. Its dependent is disabled in cascade (see Build below).
        planner.Add<LegacyImportHandler>()
            .Enabled(false)
            .DisplayName("Plan: Legacy Import");

        planner.Add<LegacyCleanupHandler>()
            .DependsOn<LegacyImportHandler>()
            .DisplayName("Plan: Legacy Cleanup");

        // With the default Build(), LegacyCleanup depending on a disabled handler would fail the startup.
        var plan = planner.Build(MissingPrerequisite.SkipDependents);

        PrintReport(plan);
        return plan;
    }

    private static void PrintReport(ExecutionPlan plan)
    {
        Console.WriteLine("Execution plan (in execution order):");

        foreach (var step in plan.Steps)
        {
            var mode = step.Prerequisites.Count == 0 ? "own cron"
                : step.RunsOnOwnSchedule ? "own cron, gated"
                : step.MinInterval.HasValue ? $"triggered, min {step.MinInterval.Value.TotalMinutes} min"
                : "triggered";

            var prerequisites = step.Prerequisites.Count == 0 ? "-" : string.Join(", ", step.Prerequisites.Select(t => t.Name));
            var status = step.Enabled ? "enabled" : "skipped: " + step.SkipReason;

            Console.WriteLine($"  {step.DisplayName,-24} {mode,-26} after: {prerequisites,-40} {status}");
        }
    }
}
