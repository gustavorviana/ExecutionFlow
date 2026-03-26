using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;
using System.ComponentModel;

namespace ExecutionFlow.Hangfire.Tests;

public class HangfireSetupTests
{
    [Fact]
    public void Configure_Registers_EventHandler()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(OrderCreatedHandler));
        });

        var registration = setup.EventHandlers.Values.Single();
        Assert.Equal(typeof(OrderCreatedHandler), registration.HandlerType);
        Assert.Equal(typeof(OrderCreatedEvent), registration.EventType);
        Assert.IsNotType<RecurringJobRegistryInfo>(registration);
    }

    [Fact]
    public void Configure_Registers_RecurringHandler()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(CleanupHandler));
        });

        var registration = setup.RecurringHandlers.Single().Value;
        Assert.Equal(typeof(CleanupHandler), registration.HandlerType);
        Assert.IsType<RecurringJobRegistryInfo>(registration);
        Assert.Equal("0 * * * *", registration.Cron);
        Assert.Equal("Cleanup Old Records", registration.DisplayName);
    }

    [Fact]
    public void Configure_Scan_Finds_All_Handlers()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Scan(typeof(HangfireSetupTests).Assembly);
        });

        Assert.Contains(setup.EventHandlers.Values, r => r.HandlerType == typeof(OrderCreatedHandler));
        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(CleanupHandler));
    }

    [Fact]
    public void Configure_Can_Mix_Scan_And_Add()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Scan(typeof(HangfireSetupTests).Assembly);
            options.Add(typeof(InlineHandler));
        });

        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(InlineHandler));
    }

    [Fact]
    public void Registrations_Distinguishes_Recurring_From_Event()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(OrderCreatedHandler));
            options.Add(typeof(CleanupHandler));
        });

        var eventHandlers = setup.EventHandlers.Values.ToList();
        var recurringHandlers = setup.RecurringHandlers.Values.ToList();

        Assert.Single(eventHandlers);
        Assert.Single(recurringHandlers);
        Assert.Equal(typeof(OrderCreatedHandler), eventHandlers[0].HandlerType);
        Assert.Equal(typeof(CleanupHandler), recurringHandlers[0].HandlerType);
    }

    // --- Build() tests ---

    private static (JobStorage storage, IStorageConnection connection, IBackgroundJobClient jobClient) CreateHangfireMocks()
    {
        var storage = Substitute.For<JobStorage>();
        var connection = Substitute.For<IStorageConnection>();
        storage.GetConnection().Returns(connection);
        var jobClient = Substitute.For<IBackgroundJobClient>();
        return (storage, connection, jobClient);
    }

    [Fact]
    public void Build_Returns_NonNull_Dispatcher()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, jobClient) = CreateHangfireMocks();

        var dispatcher = setup.Build(jobClient, storage);

        Assert.NotNull(dispatcher);
        Assert.IsAssignableFrom<IEventDispatcher>(dispatcher);
    }

    [Fact]
    public void Build_Throws_When_Called_Twice()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, jobClient) = CreateHangfireMocks();

        setup.Build(jobClient, storage);

        Assert.Throws<InvalidOperationException>(() => setup.Build(jobClient, storage));
    }

    [Fact]
    public void Build_WithCustomServiceProvider_Uses_It()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, jobClient) = CreateHangfireMocks();

        var flowActivator = new FlowEngineJobActivator(setup);
        var dispatcher = setup.Build(jobClient, storage, flowActivator);

        Assert.NotNull(dispatcher);
        Assert.NotNull(setup.JobIdGenerator);
        Assert.NotNull(setup.JobNameGenerator);
    }

    [Fact]
    public void ConfigureActivator_Sets_JobActivator_Current()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });

        setup.ConfigureActivator();

        Assert.IsType<FlowEngineJobActivator>(JobActivator.Current);
    }

    // --- BuildDispatcherOnly tests ---

    [Fact]
    public void BuildDispatcherOnly_Returns_NonNull_Dispatcher()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, jobClient) = CreateHangfireMocks();

        var dispatcher = setup.BuildDispatcherOnly(jobClient, storage);

        Assert.NotNull(dispatcher);
        Assert.IsAssignableFrom<IEventDispatcher>(dispatcher);
    }

    [Fact]
    public void BuildDispatcherOnly_Throws_When_Called_Twice()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, jobClient) = CreateHangfireMocks();

        setup.BuildDispatcherOnly(jobClient, storage);

        Assert.Throws<InvalidOperationException>(() => setup.BuildDispatcherOnly(jobClient, storage));
    }

    [Fact]
    public void BuildDispatcherOnly_Throws_ForNullJobClient()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, _) = CreateHangfireMocks();

        Assert.Throws<ArgumentNullException>(() => setup.BuildDispatcherOnly((IBackgroundJobClient)null!, storage));
    }

    [Fact]
    public void BuildDispatcherOnly_Throws_ForNullStorage()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (_, _, jobClient) = CreateHangfireMocks();

        Assert.Throws<ArgumentNullException>(() => setup.BuildDispatcherOnly(jobClient, (JobStorage)null!));
    }

    [Fact]
    public void BuildDispatcherOnly_WithStorageOnly_Returns_Dispatcher()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var (storage, _, _) = CreateHangfireMocks();

        var dispatcher = setup.BuildDispatcherOnly(storage);

        Assert.NotNull(dispatcher);
        Assert.IsAssignableFrom<IEventDispatcher>(dispatcher);
    }

    [Fact]
    public void BuildDispatcherOnly_CanPublish()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => opts.Add(typeof(OrderCreatedHandler)));
        var (storage, _, jobClient) = CreateHangfireMocks();
        jobClient.Create(default, default).ReturnsForAnyArgs("job-1");

        var dispatcher = setup.BuildDispatcherOnly(jobClient, storage);
        var result = dispatcher.Publish(new OrderCreatedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("job-1", result.JobId);
    }

    // --- Test types ---

    public class OrderCreatedEvent { }

    public class OrderCreatedHandler : IHandler<OrderCreatedEvent>
    {
        public Task HandleAsync(FlowContext<OrderCreatedEvent> context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Recurring("0 * * * *")]
    [DisplayName("Cleanup Old Records")]
    public class CleanupHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public class InlineHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
