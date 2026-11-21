using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>
/// Attributes only. The handlers declare their schedules and dependencies on the class ([DependsOn], [DependentRun]),
/// and registering them is enough: the library forms the automatic plan.
/// </summary>
public class AttributesOnlyExample : IConfigurationExample
{
    public string Title => "1. Attributes only (automatic plan)";

    public void Configure(HangfireOptions options)
    {
        options.Add(typeof(CategorySyncHandler));
        options.Add(typeof(BrandSyncHandler));
        options.Add(typeof(ProductSyncHandler));
        options.Add(typeof(DailyReportHandler));
        options.Add(typeof(PriceAuditHandler));
    }

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.True("an automatic plan was formed from the attributes", result.Plan != null);
        expect.Equal("ProductSync waits for", "CategorySyncHandler, BrandSyncHandler", result.PrerequisitesOf<ProductSyncHandler>());
        expect.Equal("ProductSync runs", "triggered, at least 00:20:00 apart", result.RunModeOf<ProductSyncHandler>());
        expect.Equal("DailyReport runs", "own schedule", result.RunModeOf<DailyReportHandler>());
        expect.Equal("BrandSync interval", TimeSpan.FromMinutes(90), result.Registration<BrandSyncHandler>().Interval);
        expect.Equal("CategorySync overlap timeout", TimeSpan.FromMinutes(5), result.Step<CategorySyncHandler>().OverlapTimeout);
    }
}
