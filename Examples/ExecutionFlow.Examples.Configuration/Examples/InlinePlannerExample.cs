using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>
/// A user plan written inline with ExecutionPlanner. A handler in it ignores its GetPlanOf values as a whole (with a
/// trace warning); its attributes still apply to anything the planner doesn't set.
/// </summary>
public class InlinePlannerExample : IConfigurationExample
{
    public string Title => "3. Inline planner (user plan)";

    public void Configure(HangfireOptions options)
    {
        var planner = new ExecutionPlanner();

        // Keeps the attribute's cron, ID and NoOverlap.
        planner.Add<CategorySyncHandler>();

        planner.Add<BrandSyncHandler>()
            .Interval(TimeSpan.FromHours(2));

        // [DependsOn] declares CategorySync and BrandSync; the plan keeps only CategorySync.
        planner.Add<ProductSyncHandler>()
            .DependsOn<CategorySyncHandler>()
            .RemoveDependency<BrandSyncHandler>()
            .RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(30))
            .DisplayName("Plan: Product Sync")
            .Queue("catalog");

        planner.Add<DailyReportHandler>()
            .Enabled(false);

        options.UsePlan(planner.Build(MissingPrerequisite.Throw));

        // Ignored: CategorySync is in the user plan.
        options.GetPlanOf<CategorySyncHandler>().Enabled(false);
    }

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.Equal("CategorySync enabled (the plan replaces GetPlanOf)", true, result.Step<CategorySyncHandler>().Enabled);
        expect.Equal("ProductSync waits for", "CategorySyncHandler", result.PrerequisitesOf<ProductSyncHandler>());
        expect.Equal("ProductSync runs", "triggered, at least 00:30:00 apart", result.RunModeOf<ProductSyncHandler>());
        expect.Equal("BrandSync interval (the planner wins)", TimeSpan.FromHours(2), result.Registration<BrandSyncHandler>().Interval);
        expect.Equal("CategorySync cron (kept from the attribute)", "*/10 * * * *", result.Registration<CategorySyncHandler>().Cron);
        expect.Equal("DailyReport enabled", false, result.Step<DailyReportHandler>().Enabled);
    }
}
