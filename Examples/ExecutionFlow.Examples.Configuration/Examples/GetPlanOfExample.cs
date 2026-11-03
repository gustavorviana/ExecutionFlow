using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>
/// Options layer. GetPlanOf&lt;T&gt;() changes a handler in the options, without writing a plan. Each value wins over the
/// class's attribute; what isn't set keeps the attribute.
/// </summary>
public class GetPlanOfExample : IConfigurationExample
{
    public string Title => "2. GetPlanOf (options layer)";

    public void Configure(HangfireOptions options)
    {
        options.Add(typeof(CategorySyncHandler));
        options.Add(typeof(BrandSyncHandler));
        options.Add(typeof(ProductSyncHandler));
        options.Add(typeof(DailyReportHandler));
        options.Add(typeof(PriceAuditHandler));

        options.GlobalRecurringAutoRun = true;
        options.RecurringTimeZone = "America/Sao_Paulo";
        options.AutomaticPlanMissingPrerequisite = MissingPrerequisite.SkipDependents;

        options.GetPlanOf<PriceAuditHandler>()
            .Enabled(false);

        options.GetPlanOf<CategorySyncHandler>()
            .Cron("*/5 * * * *")
            .NoOverlap(TimeSpan.FromMinutes(2));

        // OwnSchedule needs a schedule of its own, so it gets a cron too.
        options.GetPlanOf<ProductSyncHandler>()
            .RunMode(DependentRunMode.OwnSchedule)
            .Cron("0 * * * *")
            .Queue("catalog")
            .AutomaticRetry(3, 60, 300);
    }

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.Equal("PriceAudit enabled", false, result.Step<PriceAuditHandler>().Enabled);
        expect.Equal("CategorySync cron (from GetPlanOf)", "*/5 * * * *", result.Registration<CategorySyncHandler>().Cron);
        expect.Equal("CategorySync ID (kept from the attribute)", "category-sync", result.Registration<CategorySyncHandler>().Id);
        expect.Equal("CategorySync overlap timeout", TimeSpan.FromMinutes(2), result.Step<CategorySyncHandler>().OverlapTimeout);
        expect.Equal("ProductSync runs (GetPlanOf replaces [DependentRun])", "own schedule", result.RunModeOf<ProductSyncHandler>());
        expect.True("ProductSync has a Hangfire queue", result.Step<ProductSyncHandler>().Settings.ContainsKey("Hangfire:Queue"));
    }
}
