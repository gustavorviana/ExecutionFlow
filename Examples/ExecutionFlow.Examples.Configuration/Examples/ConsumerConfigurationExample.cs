using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Examples.Handlers;
using ExecutionFlow.Examples.Handlers.Hooks;
using ExecutionFlow.Examples.Shared.Events;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Examples;

/// <summary>The Consumer's real configuration (Examples/ExecutionFlow.Examples.Handlers/OptionsConfigurator.cs).</summary>
public class ConsumerConfigurationExample : IConfigurationExample
{
    public string Title => "6. The Consumer's OptionsConfigurator";

    public void Configure(HangfireOptions options)
    {
        OptionsConfigurator.Configure(options);
    }

    public void Verify(Expectations expect, ResolvedConfiguration result)
    {
        expect.True("the catalog plan is registered", result.Plan != null);
        expect.True("OrderCreatedEvent has a handler", result.Setup.EventHandlers.ContainsKey(typeof(OrderCreatedEvent)));
        expect.True("the JobMonitor hooks are registered", result.Setup.Options.StateHandlerTypes.Contains(typeof(JobMonitor)));
    }
}
