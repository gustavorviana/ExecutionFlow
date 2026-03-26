using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire.Common;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

public class DefaultHangfireJobNameTests
{
    private readonly IJobIdGenerator _idGenerator = Substitute.For<IJobIdGenerator>();
    private readonly IExecutionFlowRegistry _registry = Substitute.For<IExecutionFlowRegistry>();

    [Fact]
    public void GetName_ReturnsDisplayName_ForRegisteredEventHandler()
    {
        var job = JobBuilder.CreateEventJob(new TestEvent());
        var eventHandlers = new Dictionary<Type, EventJobRegistryInfo>
        {
            [typeof(TestEvent)] = new EventJobRegistryInfo(typeof(TestEventHandler), typeof(TestEvent), "My Event Handler")
        };
        _registry.EventHandlers.Returns(eventHandlers);
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());

        var jobName = new DefaultHangfireJobName(_idGenerator, _registry);

        var name = jobName.GetName(job);

        Assert.Equal("My Event Handler", name);
    }

    [Fact]
    public void GetName_ReturnsDisplayName_ForRegisteredRecurringHandler()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));
        var recurringHandlers = new Dictionary<Type, RecurringJobRegistryInfo>
        {
            [typeof(TestRecurringHandler)] = new RecurringJobRegistryInfo(typeof(TestRecurringHandler), "My Recurring", "* * * * *")
        };
        _registry.RecurringHandlers.Returns(recurringHandlers);
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());

        var jobName = new DefaultHangfireJobName(_idGenerator, _registry);

        var name = jobName.GetName(job);

        Assert.Equal("My Recurring", name);
    }

    [Fact]
    public void GetName_FallsBackToIdGenerator_WhenNotRegistered()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());
        _idGenerator.GenerateId(job.Method.DeclaringType).Returns("fallback-name");

        var jobName = new DefaultHangfireJobName(_idGenerator, _registry);

        var name = jobName.GetName(job);

        Assert.Equal("fallback-name", name);
    }

    // Test types

    public class TestEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
