using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class HangfireJobDispatcherTests
{
    // --- Helper: creates a mock PerformContext ---
    private static PerformContext CreatePerformContext()
    {
        var connection = Substitute.For<IStorageConnection>();
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(connection);

        var job = global::Hangfire.Common.Job.FromExpression(() => System.Console.WriteLine("test"));
        var bgJob = new BackgroundJob("test-job-1", job, DateTime.UtcNow);

        return new PerformContext(storage, connection, bgJob, Substitute.For<IJobCancellationToken>());
    }

    // ==========================================
    // WITHOUT DI (FlowEngineJobActivator)
    // ==========================================

    [Fact]
    public async Task WithoutDI_DispatchRecurringAsync_ExecutesHandler()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => opts.Add(typeof(TestRecurringHandler)));

        var activator = new FlowEngineJobActivator(setup);
        activator.RegisterLoggerFactory(setup.LoggerFactoryTypes);

        var dispatcher = new HangfireJobDispatcher(activator, setup);

        await dispatcher.DispatchRecurringAsync(CreatePerformContext(), typeof(TestRecurringHandler), CancellationToken.None);

        Assert.True(TestRecurringHandler.WasCalled);
    }

    [Fact]
    public async Task WithoutDI_DispatchEventAsync_ExecutesHandler()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => opts.Add(typeof(TestEventHandler)));

        var activator = new FlowEngineJobActivator(setup);
        activator.RegisterLoggerFactory(setup.LoggerFactoryTypes);

        var dispatcher = new HangfireJobDispatcher(activator, setup);
        var evt = new TestEvent { Message = "hello" };

        await dispatcher.DispatchEventAsync(evt, null, CreatePerformContext(), CancellationToken.None);

        Assert.True(TestEventHandler.WasCalled);
        Assert.Equal("hello", TestEventHandler.ReceivedMessage);
    }

    [Fact]
    public async Task WithoutDI_DispatchEventAsync_SetsCustomId()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => opts.Add(typeof(CustomIdEventHandler)));

        var activator = new FlowEngineJobActivator(setup);
        activator.RegisterLoggerFactory(setup.LoggerFactoryTypes);

        var dispatcher = new HangfireJobDispatcher(activator, setup);

        await dispatcher.DispatchEventAsync(new CustomIdEvent(), null, CreatePerformContext(), CancellationToken.None);

        Assert.Equal("my-custom-id", CustomIdEventHandler.ReceivedCustomId);
    }

    [Fact]
    public async Task WithoutDI_DispatchRecurringAsync_Throws_WhenHandlerNotResolvable()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => opts.Add(typeof(TestRecurringHandler)));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(TestRecurringHandler)).Returns(null);
        serviceProvider.GetService(typeof(ExecutionLoggerFactory)).Returns(new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>()));

        var dispatcher = new HangfireJobDispatcher(serviceProvider, setup);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchRecurringAsync(CreatePerformContext(), typeof(TestRecurringHandler), CancellationToken.None));
    }

    [Fact]
    public async Task WithoutDI_DispatchEventAsync_Throws_WhenEventNotRegistered()
    {
        var setup = new HangfireSetup();
        setup.Configure(opts => { });

        var activator = new FlowEngineJobActivator(setup);
        activator.RegisterLoggerFactory(setup.LoggerFactoryTypes);

        var dispatcher = new HangfireJobDispatcher(activator, setup);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchEventAsync(new TestEvent(), null, CreatePerformContext(), CancellationToken.None));
    }

    // ==========================================
    // WITH DI (Microsoft.Extensions.DependencyInjection)
    // ==========================================

    [Fact]
    public async Task WithDI_DispatchRecurringAsync_ExecutesHandler()
    {
        using var sp = BuildServiceProvider(opts => opts.Add(typeof(TestRecurringHandler)));

        var dispatcher = new HangfireJobDispatcher(sp, sp.GetRequiredService<IExecutionFlowRegistry>());

        await dispatcher.DispatchRecurringAsync(CreatePerformContext(), typeof(TestRecurringHandler), CancellationToken.None);

        Assert.True(TestRecurringHandler.WasCalled);
    }

    [Fact]
    public async Task WithDI_DispatchEventAsync_ExecutesHandler()
    {
        using var sp = BuildServiceProvider(opts => opts.Add(typeof(TestEventHandler)));

        var dispatcher = new HangfireJobDispatcher(sp, sp.GetRequiredService<IExecutionFlowRegistry>());
        var evt = new TestEvent { Message = "from DI" };

        await dispatcher.DispatchEventAsync(evt, null, CreatePerformContext(), CancellationToken.None);

        Assert.True(TestEventHandler.WasCalled);
        Assert.Equal("from DI", TestEventHandler.ReceivedMessage);
    }

    [Fact]
    public async Task WithDI_DispatchEventAsync_HandlerWithDependency_ResolvesCorrectly()
    {
        using var sp = BuildServiceProvider(opts => opts.Add(typeof(HandlerWithDependency)), services =>
        {
            services.AddSingleton(new SomeDependency { Value = "injected" });
        });

        var dispatcher = new HangfireJobDispatcher(sp, sp.GetRequiredService<IExecutionFlowRegistry>());

        await dispatcher.DispatchEventAsync(new DependencyEvent(), null, CreatePerformContext(), CancellationToken.None);

        Assert.True(HandlerWithDependency.WasCalled);
        Assert.Equal("injected", HandlerWithDependency.ReceivedValue);
    }

    // --- Helper: builds a ServiceProvider with ExecutionFlow configured ---

    private static ServiceProvider BuildServiceProvider(Action<HangfireOptions> configure, Action<IServiceCollection>? extraServices = null)
    {
        var services = new ServiceCollection();

        var setup = new HangfireSetup();
        setup.Configure(configure);

        foreach (var reg in setup.RecurringHandlers.Values)
            services.AddTransient(reg.HandlerType);
        foreach (var reg in setup.EventHandlers.Values)
            services.AddTransient(reg.HandlerType);

        services.AddSingleton<ExecutionLoggerFactory>(new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>()));
        services.AddSingleton<IExecutionFlowRegistry>(setup);

        extraServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    // ==========================================
    // Test types (static flags for assertion)
    // ==========================================

    public class TestEvent
    {
        public string Message { get; set; } = "";
    }

    public class TestRecurringHandler : IHandler
    {
        public static bool WasCalled;

        public TestRecurringHandler() => WasCalled = false;

        public Task HandleAsync(FlowContext context, CancellationToken ct)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public static bool WasCalled;
        public static string? ReceivedMessage;

        public TestEventHandler() { WasCalled = false; ReceivedMessage = null; }

        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct)
        {
            WasCalled = true;
            ReceivedMessage = context.Event.Message;
            return Task.CompletedTask;
        }
    }

    public class CustomIdEvent : ICustomIdEvent
    {
        public string CustomId => "my-custom-id";
    }

    public class CustomIdEventHandler : IHandler<CustomIdEvent>
    {
        public static string? ReceivedCustomId;

        public CustomIdEventHandler() => ReceivedCustomId = null;

        public Task HandleAsync(FlowContext<CustomIdEvent> context, CancellationToken ct)
        {
            ReceivedCustomId = context.CustomId;
            return Task.CompletedTask;
        }
    }

    public class SomeDependency
    {
        public string Value { get; set; } = "";
    }

    public class DependencyEvent { }

    public class HandlerWithDependency : IHandler<DependencyEvent>
    {
        public static bool WasCalled;
        public static string? ReceivedValue;

        private readonly SomeDependency _dep;

        public HandlerWithDependency(SomeDependency dep)
        {
            _dep = dep;
            WasCalled = false;
            ReceivedValue = null;
        }

        public Task HandleAsync(FlowContext<DependencyEvent> context, CancellationToken ct)
        {
            WasCalled = true;
            ReceivedValue = _dep.Value;
            return Task.CompletedTask;
        }
    }
}
