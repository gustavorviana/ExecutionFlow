using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Configurations;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>The shortcut for a plan made only of configuration classes: no planner in sight.</summary>
public class UsePlanFromAssemblyExample : IConfigurationExample
{
    public string Title => "5. UsePlanFromAssembly";

    public void Configure(HangfireOptions options)
    {
        options.UsePlanFromAssembly(
            typeof(ProductSyncConfiguration).Assembly,
            ConfigurationClassesExample.IsCatalogConfiguration,
            MissingPrerequisite.Throw);
    }

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.Equal("handlers in the plan", 2, result.Plan!.Steps.Count);
        expect.Equal("ProductSync overlap timeout (AllowOverlap)", null, result.Step<ProductSyncHandler>().OverlapTimeout);
    }
}
