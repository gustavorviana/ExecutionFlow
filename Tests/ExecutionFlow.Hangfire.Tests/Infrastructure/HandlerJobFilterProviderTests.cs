using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Common;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class HandlerJobFilterProviderTests
{
    private static HandlerJobFilterProvider CreateProvider(
        Action<HangfireOptions> configure,
        IExecutionFlowRegistry? registry = null)
    {
        var options = new HangfireOptions();
        configure(options);

        return new HandlerJobFilterProvider(
            registry ?? Substitute.For<IExecutionFlowRegistry>(),
            options);
    }

    private static IExecutionFlowRegistry CreateRegistryWith<THandler>() where THandler : IHandler
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>
        {
            [typeof(THandler)] = new RecurringJobRegistryInfo(typeof(THandler), typeof(THandler).Name, null)
        });
        registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());
        return registry;
    }

    private static IExecutionFlowRegistry CreateRegistryWithEvent<THandler, TEvent>()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>
        {
            [typeof(TEvent)] = new EventJobRegistryInfo(typeof(THandler), typeof(TEvent), typeof(THandler).Name)
        });
        registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        return registry;
    }

    // --- DisableRecurringRetries default ---

    [Fact]
    public void DisableRecurringRetries_DefaultsToTrue()
    {
        var options = new HangfireOptions();

        Assert.True(options.DisableRecurringRetries);
    }

    // --- Recurring job + DisableRecurringRetries = true ---

    [Fact]
    public void RecurringJob_GetsRetryDisabled_WhenDisableRecurringRetriesIsTrue()
    {
        var registry = CreateRegistryWith<TestRecurringHandler>();
        var provider = CreateProvider(opts => { }, registry);
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));

        var filters = provider.GetFilters(job).ToList();

        var retryFilter = filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().SingleOrDefault();
        Assert.NotNull(retryFilter);
        Assert.Equal(0, retryFilter!.Attempts);
    }

    // --- Recurring job + DisableRecurringRetries = false ---

    [Fact]
    public void RecurringJob_DoesNotGetRetryDisabled_WhenDisableRecurringRetriesIsFalse()
    {
        var registry = CreateRegistryWith<TestRecurringHandler>();
        var provider = CreateProvider(opts => opts.DisableRecurringRetries = false, registry);
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));

        var filters = provider.GetFilters(job).ToList();

        var retryFilter = filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().SingleOrDefault();
        Assert.Null(retryFilter);
    }

    // --- Handler with custom [AutomaticRetry] is not overridden ---

    [Fact]
    public void RecurringJob_WithCustomRetryAttribute_IsNotOverridden()
    {
        var registry = CreateRegistryWith<CustomRetryHandler>();
        var provider = CreateProvider(opts => { }, registry);
        var job = JobBuilder.CreateRecurringJob(typeof(CustomRetryHandler));

        var filters = provider.GetFilters(job).ToList();

        var retryFilters = filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().ToList();
        Assert.Single(retryFilters);
        Assert.Equal(5, retryFilters[0].Attempts);
    }

    // --- Event job is not affected ---

    [Fact]
    public void EventJob_IsNotAffected_ByDisableRecurringRetries()
    {
        var registry = CreateRegistryWithEvent<TestEventHandler, TestEvent>();
        var provider = CreateProvider(opts => { }, registry);
        var job = JobBuilder.CreateEventJob(new TestEvent());

        var filters = provider.GetFilters(job).ToList();

        var retryFilter = filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().SingleOrDefault();
        Assert.Null(retryFilter);
    }

    // --- Null job returns empty ---

    [Fact]
    public void GetFilters_ReturnsEmpty_ForNullJob()
    {
        var provider = CreateProvider(opts => { });

        var filters = provider.GetFilters(null!);

        Assert.Empty(filters);
    }

    // --- Unregistered handler returns empty ---

    [Fact]
    public void GetFilters_ReturnsEmpty_WhenHandlerNotRegistered()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());

        var provider = CreateProvider(opts => { }, registry);
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));

        var filters = provider.GetFilters(job);

        Assert.Empty(filters);
    }

    // Test types

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 5)]
    public class CustomRetryHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    public class TestEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }
}
