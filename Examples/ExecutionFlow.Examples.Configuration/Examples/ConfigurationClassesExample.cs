using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Configurations;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>
/// Configuration classes (like EF Core's IEntityTypeConfiguration&lt;T&gt;), applied on a planner and mixed with inline
/// configuration. See <see cref="ProductSyncConfiguration"/> and <see cref="CategorySyncConfiguration"/>.
/// </summary>
public class ConfigurationClassesExample : IConfigurationExample
{
    public string Title => "4. Configuration classes + inline";

    public void Configure(HangfireOptions options)
    {
        var planner = new ExecutionPlanner();
        planner.ApplyConfigurationsFromAssembly(typeof(ProductSyncConfiguration).Assembly, IsCatalogConfiguration);
        planner.Add<BrandSyncHandler>();

        options.UsePlan(planner.Build());
    }

    /// <summary>Only the classes of the Configurations namespace: each provider's plan picks its own classes.</summary>
    public static bool IsCatalogConfiguration(Type type) => type.Namespace == typeof(ProductSyncConfiguration).Namespace;

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.Equal("ProductSync interval (configuration class)", TimeSpan.FromMinutes(45), result.Registration<ProductSyncHandler>().Interval);
        expect.Equal("ProductSync ID (configuration class)", "product-sync", result.Registration<ProductSyncHandler>().Id);
        expect.Equal("ProductSync waits for (ClearDependencies + DependsOn)", "CategorySyncHandler", result.PrerequisitesOf<ProductSyncHandler>());
        expect.Equal("ProductSync runs", "own schedule", result.RunModeOf<ProductSyncHandler>());
        expect.Equal("CategorySync time zone (configuration class)", "Europe/Lisbon", result.Step<CategorySyncHandler>().TimeZone);
        expect.Equal("CategorySync cron (kept from the attribute)", "*/10 * * * *", result.Registration<CategorySyncHandler>().Cron);
    }
}
