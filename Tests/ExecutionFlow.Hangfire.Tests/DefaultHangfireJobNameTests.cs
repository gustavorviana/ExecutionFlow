using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire.Common;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

public class DefaultHangfireJobNameTests
{
    private readonly IJobIdGenerator _idGenerator = Substitute.For<IJobIdGenerator>();

    public DefaultHangfireJobNameTests()
    {
        // Makes names that came from the ID generator easy to recognize in assertions.
        _idGenerator.GenerateId(Arg.Any<Type>()).Returns(ci => "id:" + ci.Arg<Type>().Name);
    }
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
    public void GetName_FallsBackToIdGenerator_ForRecurringHandler_WhenNotRegistered()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());

        var jobName = new DefaultHangfireJobName(_idGenerator, _registry);

        var name = jobName.GetName(job);

        // The recurring job carries its handler type in the args, so the generated name comes from
        // the handler type even when the registry (e.g. on a producer host) is empty.
        Assert.Equal("id:" + nameof(TestRecurringHandler), name);
    }

    [Fact]
    public void GetName_FallsBackToIdGenerator_ForEventType_WhenHandlerNotRegistered()
    {
        var job = JobBuilder.CreateEventJob(new TestEvent());
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());

        var jobName = new DefaultHangfireJobName(_idGenerator, _registry);

        var name = jobName.GetName(job);

        Assert.Equal("id:" + nameof(TestEvent), name);
    }

    [Fact]
    public void GetName_FallsBackToIdGenerator_ForRegisteredHandler_WithoutDefinedName()
    {
        var name = CreateWithEventHandler<TestEventHandler, TestEvent>().GetName(JobBuilder.CreateEventJob(new TestEvent()));

        Assert.Equal("id:" + nameof(TestEventHandler), name);
    }

    [Fact]
    public void GetName_UsesEventDisplayName_WhenHandlerNotRegistered()
    {
        var name = CreateWithEmptyRegistry().GetName(JobBuilder.CreateEventJob(new NamedEvent()));

        Assert.Equal("Named event", name);
    }

    // --- Non-ExecutionFlow jobs: [JobDisplayName], otherwise the ID generator ---

    private DefaultHangfireJobName CreateWithEmptyRegistry()
    {
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());
        return new DefaultHangfireJobName(_idGenerator, _registry);
    }

    [Fact]
    public void GetName_NativeJob_FallsBackToIdGenerator()
    {
        var job = Job.FromExpression(() => NativeJobs.Cleanup());

        var name = CreateWithEmptyRegistry().GetName(job);

        Assert.Equal("id:" + nameof(NativeJobs), name);
    }

    [Fact]
    public void GetName_GenericNativeJob_IsNotTreatedAsExecutionFlowEvent()
    {
        var job = Job.FromExpression(() => NativeJobs.Process<TestEvent>());

        var name = CreateWithEmptyRegistry().GetName(job);

        Assert.Equal("id:" + nameof(NativeJobs), name);
    }

    [Fact]
    public void GetName_NativeJob_UsesHangfireJobDisplayName_WithArguments()
    {
        var job = Job.FromExpression(() => NativeJobs.Import(5));

        Assert.Equal("Import batch 5", CreateWithEmptyRegistry().GetName(job));
    }

    // --- [JobDisplayName] on ExecutionFlow handlers (to-be item 4) ---

    private DefaultHangfireJobName CreateWithEventHandler<THandler, TEvent>()
    {
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>
        {
            // Same display name ExecutionFlowOptions.Add computes: [DisplayName] or the class name.
            [typeof(TEvent)] = new EventJobRegistryInfo(typeof(THandler), typeof(TEvent),
                typeof(THandler).GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), false).OfType<System.ComponentModel.DisplayNameAttribute>().FirstOrDefault()?.DisplayName ?? typeof(THandler).Name)
        });
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
        return new DefaultHangfireJobName(_idGenerator, _registry);
    }

    [Fact]
    public void GetName_UsesHandlerJobDisplayName_WithEventAsPlaceholder()
    {
        var name = CreateWithEventHandler<JobDisplayNameHandler, ReminderEvent>()
            .GetName(JobBuilder.CreateEventJob(new ReminderEvent { OrderId = "42" }));

        Assert.Equal("Reminder for order 42", name);
    }

    [Fact]
    public void GetName_JobDisplayName_WinsOverHandlerDisplayName()
    {
        // [JobDisplayName] on HandleAsync comes before the handler class's [DisplayName].
        var name = CreateWithEventHandler<BothAttributesHandler, ReminderEvent>()
            .GetName(JobBuilder.CreateEventJob(new ReminderEvent { OrderId = "42" }));

        Assert.Equal("Reminder for order 42", name);
    }

    [Fact]
    public void GetName_UsesHandlerDisplayName_WhenNoJobDisplayName()
    {
        var name = CreateWithEventHandler<DisplayNameOnlyHandler, ReminderEvent>()
            .GetName(JobBuilder.CreateEventJob(new ReminderEvent { OrderId = "42" }));

        Assert.Equal("Explicit name", name);
    }

    [Fact]
    public void GetName_JobDisplayNameWithUnknownPlaceholder_UsesTextAsIs()
    {
        var name = CreateWithEventHandler<UnknownPlaceholderHandler, ReminderEvent>()
            .GetName(JobBuilder.CreateEventJob(new ReminderEvent { OrderId = "42" }));

        Assert.Equal("Reminder {1}", name);
    }

    [Fact]
    public void GetName_CustomName_WinsOverJobDisplayName()
    {
        var method = typeof(HangfireJobDispatcher).GetMethod(nameof(HangfireJobDispatcher.DispatchEventAsync))!.MakeGenericMethod(typeof(ReminderEvent));
        var job = new Job(typeof(HangfireJobDispatcher), method, new object?[] { new ReminderEvent { OrderId = "42" }, "Custom", null, CancellationToken.None });

        var name = CreateWithEventHandler<JobDisplayNameHandler, ReminderEvent>().GetName(job);

        Assert.Equal("Custom", name);
    }

    [Fact]
    public void GetName_NativeJob_FallbackUsesTheJobType()
    {
        // BackgroundJob.Enqueue<INativeService>(x => x.Run()): the job's type is the interface.
        var job = Job.FromExpression<INativeService>(x => x.Run());

        Assert.Equal("id:" + nameof(INativeService), CreateWithEmptyRegistry().GetName(job));
    }

    public interface INativeService
    {
        void Run();
    }

    public static class NativeJobs
    {
        public static void Cleanup() { }
        public static void Process<T>() { }

        [global::Hangfire.JobDisplayName("Import batch {0}")]
        public static void Import(int batch) { }
    }

    public class ReminderEvent
    {
        public string OrderId { get; set; } = "";
        public override string ToString() => OrderId;
    }

    // Abstract handlers: only used as types, and kept out of the assembly scans in other tests.
    public abstract class JobDisplayNameHandler : IHandler<ReminderEvent>
    {
        [global::Hangfire.JobDisplayName("Reminder for order {0}")]
        public Task HandleAsync(FlowContext<ReminderEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    [System.ComponentModel.DisplayName("Explicit name")]
    public abstract class BothAttributesHandler : IHandler<ReminderEvent>
    {
        [global::Hangfire.JobDisplayName("Reminder for order {0}")]
        public Task HandleAsync(FlowContext<ReminderEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    [System.ComponentModel.DisplayName("Explicit name")]
    public abstract class DisplayNameOnlyHandler : IHandler<ReminderEvent>
    {
        public Task HandleAsync(FlowContext<ReminderEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public abstract class UnknownPlaceholderHandler : IHandler<ReminderEvent>
    {
        [global::Hangfire.JobDisplayName("Reminder {1}")]
        public Task HandleAsync(FlowContext<ReminderEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    // Test types

    public class TestEvent { }

    [System.ComponentModel.DisplayName("Named event")]
    public class NamedEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    [ExecutionFlow.Attributes.Recurring("* * * * *")]
    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
