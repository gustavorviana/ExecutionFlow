using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Configurations;

// Configuration classes (like EF Core's IEntityTypeConfiguration<T>). Kept in their own namespace so a provider's plan
// can pick them with ApplyConfigurationsFromAssembly(assembly, predicate) or UsePlanFromAssembly(assembly, predicate).

/// <summary>
/// Replaces the class's schedule, ID, dependencies, run mode and overlap; everything not called keeps its attribute.
/// It runs every 45 minutes on its own schedule, and each occurrence only runs when CategorySync completed a new cycle.
/// (A triggered dependent ignores its own schedule, so a schedule only makes sense with <see cref="DependentRunMode.OwnSchedule"/>.)
/// </summary>
public class ProductSyncConfiguration : IHandlerConfiguration<ProductSyncHandler>
{
    public void Configure(HandlerBuilder<ProductSyncHandler> handler)
    {
        handler.Interval(TimeSpan.FromMinutes(45))
               .Id("product-sync")
               .ClearDependencies()
               .DependsOn<CategorySyncHandler>()
               .RunMode(DependentRunMode.OwnSchedule)
               .AllowOverlap()
               .Queue("catalog");
    }
}

/// <summary>Changes only the time zone; the cron, ID and NoOverlap come from the class's attributes.</summary>
public class CategorySyncConfiguration : IHandlerConfiguration<CategorySyncHandler>
{
    public void Configure(HandlerBuilder<CategorySyncHandler> handler)
    {
        handler.TimeZone("Europe/Lisbon");
    }
}
