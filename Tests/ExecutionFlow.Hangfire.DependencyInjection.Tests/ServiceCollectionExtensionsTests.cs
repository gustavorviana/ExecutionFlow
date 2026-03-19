using ExecutionFlow.Abstractions;
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

        var dispatcher = provider.GetService<IDispatcher>();

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

    // Test types

    public class TestEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
