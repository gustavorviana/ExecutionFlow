using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ExecutionFlow.Hangfire.DependencyInjection.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<HangfireOptions>? configure = null)
    {
        var services = new ServiceCollection();

        // Register Hangfire dependencies that ExecutionFlow needs
        var storage = Substitute.For<JobStorage>();
        var connection = Substitute.For<IStorageConnection>();
        storage.GetConnection().Returns(connection);

        services.AddSingleton(storage);
        services.AddSingleton(Substitute.For<IBackgroundJobClient>());
        services.AddSingleton(Substitute.For<JobActivator>());

        services.AddHangfireToExecutionFlow(configure);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registers_IDispatcher()
    {
        using var provider = BuildProvider();

        var dispatcher = provider.GetService<IEventDispatcher>();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public void Registers_IExecutionManager()
    {
        using var provider = BuildProvider();

        var manager = provider.GetService<IExecutionManager>();

        Assert.NotNull(manager);
    }

    [Fact]
    public void Registers_IHangfireJobName()
    {
        using var provider = BuildProvider();

        var jobName = provider.GetService<IHangfireJobName>();

        Assert.NotNull(jobName);
    }

    [Fact]
    public void Registers_IExecutionFlowRegistry()
    {
        using var provider = BuildProvider();

        var registry = provider.GetService<IExecutionFlowRegistry>();

        Assert.NotNull(registry);
    }

    [Fact]
    public void Registers_IRecurringTrigger()
    {
        using var provider = BuildProvider();

        var trigger = provider.GetService<IRecurringTrigger>();

        Assert.NotNull(trigger);
    }

    [Fact]
    public void WithoutOptions_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            using var provider = BuildProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ScansAssembly_RegistersDiscoveredHandlers()
    {
        using var provider = BuildProvider(options =>
            options.Scan(typeof(ServiceCollectionExtensionsTests).Assembly));

        var handler = provider.GetService<TestEventHandler>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void Registers_EventHandler_AsTransient()
    {
        using var provider = BuildProvider(options =>
            options.Add(typeof(TestEventHandler)));

        var handler1 = provider.GetService<TestEventHandler>();
        var handler2 = provider.GetService<TestEventHandler>();

        Assert.NotNull(handler1);
        Assert.NotNull(handler2);
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void Registers_RecurringHandler_AsTransient()
    {
        using var provider = BuildProvider(options =>
            options.Add(typeof(TestRecurringHandler)));

        var handler1 = provider.GetService<TestRecurringHandler>();
        var handler2 = provider.GetService<TestRecurringHandler>();

        Assert.NotNull(handler1);
        Assert.NotNull(handler2);
        Assert.NotSame(handler1, handler2);
    }

    // --- AddExecutionFlowDispatcher (producer-only) ---

    private static ServiceProvider BuildDispatcherOnlyProvider(Action<HangfireOptions>? configure = null)
    {
        var services = new ServiceCollection();
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        services.AddSingleton(storage);
        services.AddExecutionFlowDispatcher(configure);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void DispatcherOnly_DefaultOverload_ResolvesStorageFromDI()
    {
        using var provider = BuildDispatcherOnlyProvider();

        var dispatcher = provider.GetService<IEventDispatcher>();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public void DispatcherOnly_WithStorageFunc_Registers_IEventDispatcher()
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        var services = new ServiceCollection();
        services.AddExecutionFlowDispatcher(_ => storage);

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetService<IEventDispatcher>();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public void DispatcherOnly_Registers_IExecutionManager()
    {
        // Producer hosts (e.g. an API) can check, cancel and retry jobs; the manager only needs the storage.
        using var provider = BuildDispatcherOnlyProvider();

        var manager = provider.GetService<IExecutionManager>();

        Assert.NotNull(manager);
    }

    [Fact]
    public void DispatcherOnly_DoesNotRegister_IRecurringTrigger()
    {
        using var provider = BuildDispatcherOnlyProvider();

        var trigger = provider.GetService<IRecurringTrigger>();

        Assert.Null(trigger);
    }

    [Fact]
    public void DispatcherOnly_WithStorageFunc_CanPublish()
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        var services = new ServiceCollection();
        services.AddExecutionFlowDispatcher(_ => storage);

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IEventDispatcher>();

        var result = dispatcher.Publish(new TestEvent());

        Assert.NotNull(result);
    }

    // --- Registry binding (HangfireJobDispatcher / IHangfireJobName) ---

    [Fact]
    public void Registers_HangfireJobDispatcher()
    {
        using var provider = BuildProvider(options => options.Add(typeof(TestEventHandler)));

        var dispatcher = provider.GetService<ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher>();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public async Task HangfireJobDispatcher_UsesConfiguredRegistry_EvenWhenAnotherRegistryWinsInContainer()
    {
        var services = new ServiceCollection();
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        services.AddSingleton(storage);
        services.AddSingleton(Substitute.For<IBackgroundJobClient>());

        services.AddHangfireToExecutionFlow(options => options.Add(typeof(TestEventHandler)));

        // Simulates a second, handler-less registry registered later (e.g. AddExecutionFlowDispatcher
        // or any stray registration) that would previously be injected into the job dispatcher.
        services.AddSingleton(Substitute.For<IExecutionFlowRegistry>());

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher>();

        // Must find the handler registered in the configured setup, not throw "No handler registered".
        var exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchEventAsync(new TestEvent(), null, null!, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public void BothExtensions_Throw_WhenProducerOnlyIsRegisteredFirst()
    {
        var services = new ServiceCollection();
        services.AddExecutionFlowDispatcher(_ => Substitute.For<JobStorage>());

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddHangfireToExecutionFlow());

        Assert.Contains("Use only AddHangfireToExecutionFlow", ex.Message);
    }

    [Fact]
    public void BothExtensions_Throw_WhenFullModeIsRegisteredFirst()
    {
        var services = new ServiceCollection();
        services.AddHangfireToExecutionFlow();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddExecutionFlowDispatcher(_ => Substitute.For<JobStorage>()));

        Assert.Contains("Remove the AddExecutionFlowDispatcher call", ex.Message);
    }

    [Fact]
    public void StartExecutionFlow_BuildsWithoutAHost()
    {
        using var provider = BuildProvider(options => options.Add(typeof(TestRecurringHandler)));

        provider.StartExecutionFlow();

        // Build() ran: the setup's recurring registration resolved the job ID generator.
        var setup = (HangfireSetup)provider.GetRequiredService<IExecutionFlowRegistry>();
        Assert.NotNull(setup.JobIdGenerator);
    }

    [Fact]
    public void ExecutionManager_IsBoundToTheBuiltSetup()
    {
        using var provider = BuildProvider();

        var manager = provider.GetRequiredService<IExecutionManager>();
        var setup = (HangfireSetup)provider.GetRequiredService<IExecutionFlowRegistry>();

        Assert.Same(setup.ExecutionManager, manager);
    }

    // Test types

    public class TestEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [ExecutionFlow.Attributes.Recurring("* * * * *")]
    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
