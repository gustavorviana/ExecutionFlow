using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;
using NSubstitute;
using HangfireJobDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher;

namespace ExecutionFlow.Hangfire.Tests;

public class HangfireJobInfoTests
{
    private static Job CreateEventJob<TEvent>(string? customName = null)
    {
        return Job.FromExpression<HangfireJobDispatcher>(
            x => x.DispatchEventAsync<TEvent>(default!, customName, null, default));
    }

    private static Job CreateRecurringJob(Type? handlerType = null)
    {
        var method = typeof(HangfireJobDispatcher)
            .GetMethod(nameof(HangfireJobDispatcher.DispatchRecurringAsync))!;

        return new Job(
            typeof(HangfireJobDispatcher),
            method,
            new object[] { null!, handlerType!, CancellationToken.None });
    }

    // --- HangfireJobInfo.Create ---

    [Fact]
    public void Create_GenericJob_Returns_HangfireEventJobInfo()
    {
        var job = CreateEventJob<TestEvent>();

        var info = HangfireJobInfo.Create(job);

        Assert.IsType<HangfireEventJobInfo>(info);
    }

    [Fact]
    public void Create_NonGenericJob_Returns_HangfireRecurringJobInfo()
    {
        var job = CreateRecurringJob(typeof(TestHandler));

        var info = HangfireJobInfo.Create(job);

        Assert.IsType<HangfireRecurringJobInfo>(info);
    }

    // --- HangfireEventJobInfo ---

    [Fact]
    public void EventJobInfo_EventType_Extracted()
    {
        var job = CreateEventJob<TestEvent>();

        var info = new HangfireEventJobInfo(job);

        Assert.Equal(typeof(TestEvent), info.EventType);
    }

    [Fact]
    public void EventJobInfo_CustomJobName_Extracted_WhenProvided()
    {
        var job = CreateEventJob<TestEvent>("my-custom-name");

        var info = new HangfireEventJobInfo(job);

        Assert.Equal("my-custom-name", info.CustomJobName);
    }

    [Fact]
    public void EventJobInfo_CustomJobName_Null_WhenEmpty()
    {
        var job = CreateEventJob<TestEvent>("");

        var info = new HangfireEventJobInfo(job);

        Assert.Null(info.CustomJobName);
    }

    [Fact]
    public void EventJobInfo_CustomJobName_Null_WhenNotProvided()
    {
        var job = CreateEventJob<TestEvent>(null);

        var info = new HangfireEventJobInfo(job);

        Assert.Null(info.CustomJobName);
    }

    [Fact]
    public void EventJobInfo_GetHandler_Returns_EventHandler_FromRegistry()
    {
        var job = CreateEventJob<TestEvent>();
        var info = new HangfireEventJobInfo(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var expectedHandler = new EventJobRegistryInfo(typeof(TestEventHandler), typeof(TestEvent), "Test");
        var handlers = new Dictionary<Type, EventJobRegistryInfo> { { typeof(TestEvent), expectedHandler } };
        registry.EventHandlers.Returns((IReadOnlyDictionary<Type, EventJobRegistryInfo>)handlers);

        var handler = info.GetHandler(registry);

        Assert.Same(expectedHandler, handler);
    }

    [Fact]
    public void EventJobInfo_GetHandler_Returns_Null_WhenNotRegistered()
    {
        var job = CreateEventJob<TestEvent>();
        var info = new HangfireEventJobInfo(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());

        var handler = info.GetHandler(registry);

        Assert.Null(handler);
    }

    [Fact]
    public void EventJobInfo_GetExpectedName_Returns_CustomName_WhenAvailable()
    {
        var job = CreateEventJob<TestEvent>("custom-display");
        var info = new HangfireEventJobInfo(job);
        var registryInfo = new EventJobRegistryInfo(typeof(TestEventHandler), typeof(TestEvent), "Fallback Name");

        var name = info.GetExpectedName(registryInfo);

        Assert.Equal("custom-display", name);
    }

    [Fact]
    public void EventJobInfo_GetExpectedName_Falls_Back_To_DisplayName()
    {
        var job = CreateEventJob<TestEvent>(null);
        var info = new HangfireEventJobInfo(job);
        var registryInfo = new EventJobRegistryInfo(typeof(TestEventHandler), typeof(TestEvent), "Fallback Name");

        var name = info.GetExpectedName(registryInfo);

        Assert.Equal("Fallback Name", name);
    }

    // --- HangfireRecurringJobInfo ---

    [Fact]
    public void RecurringJobInfo_HandlerType_Extracted()
    {
        var job = CreateRecurringJob(typeof(TestHandler));

        var info = new HangfireRecurringJobInfo(job);

        Assert.Equal(typeof(TestHandler), info.HandlerType);
    }

    [Fact]
    public void RecurringJobInfo_GetHandler_Returns_RecurringHandler_FromRegistry()
    {
        var job = CreateRecurringJob(typeof(TestHandler));
        var info = new HangfireRecurringJobInfo(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var expectedHandler = new RecurringJobRegistryInfo(typeof(TestHandler), "Test Handler", "* * * * *");
        var handlers = new Dictionary<Type, RecurringJobRegistryInfo> { { typeof(TestHandler), expectedHandler } };
        registry.RecurringHandlers.Returns((IReadOnlyDictionary<Type, RecurringJobRegistryInfo>)handlers);

        var handler = info.GetHandler(registry);

        Assert.Same(expectedHandler, handler);
    }

    [Fact]
    public void RecurringJobInfo_GetHandler_Returns_Null_WhenNotRegistered()
    {
        var job = CreateRecurringJob(typeof(TestHandler));
        var info = new HangfireRecurringJobInfo(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());

        var handler = info.GetHandler(registry);

        Assert.Null(handler);
    }

    [Fact]
    public void RecurringJobInfo_GetJobType_Returns_Type_FromArgs()
    {
        var job = CreateRecurringJob(typeof(TestHandler));

        var type = HangfireRecurringJobInfo.GetJobType(job);

        Assert.Equal(typeof(TestHandler), type);
    }

    // --- HangfireJobInfo base ---

    [Fact]
    public void GetHandlerType_Returns_HandlerType_ViaRegistry()
    {
        var job = CreateEventJob<TestEvent>();
        var info = HangfireJobInfo.Create(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var handler = new EventJobRegistryInfo(typeof(TestEventHandler), typeof(TestEvent), "Test");
        var handlers = new Dictionary<Type, EventJobRegistryInfo> { { typeof(TestEvent), handler } };
        registry.EventHandlers.Returns((IReadOnlyDictionary<Type, EventJobRegistryInfo>)handlers);

        var handlerType = info.GetHandlerType(registry);

        Assert.Equal(typeof(TestEventHandler), handlerType);
    }

    [Fact]
    public void GetExpectedName_Returns_DisplayName()
    {
        var job = CreateRecurringJob(typeof(TestHandler));
        var info = HangfireJobInfo.Create(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var handler = new RecurringJobRegistryInfo(typeof(TestHandler), "My Display Name", "* * * * *");
        var handlers = new Dictionary<Type, RecurringJobRegistryInfo> { { typeof(TestHandler), handler } };
        registry.RecurringHandlers.Returns((IReadOnlyDictionary<Type, RecurringJobRegistryInfo>)handlers);

        var name = info.GetExpectedName(registry);

        Assert.Equal("My Display Name", name);
    }

    [Fact]
    public void GetExpectedName_Falls_Back_To_FullName_WhenDisplayNameEmpty()
    {
        var job = CreateRecurringJob(typeof(TestHandler));
        var info = HangfireJobInfo.Create(job);
        var registryInfo = new RecurringJobRegistryInfo(typeof(TestHandler), "", "* * * * *");

        var name = info.GetExpectedName(registryInfo);

        Assert.Equal(typeof(TestHandler).FullName, name);
    }

    // --- Test types ---

    public class TestEvent { }

    public class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
