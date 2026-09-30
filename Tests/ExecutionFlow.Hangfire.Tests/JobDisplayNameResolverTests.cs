using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire.Common;
using NSubstitute;
using System.ComponentModel;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>job-naming-and-dashboard REQ-001: the fixed ExecutionFlow rules, without Hangfire's attributes.</summary>
public class JobDisplayNameResolverTests
{
    private readonly IJobIdGenerator _idGenerator = Substitute.For<IJobIdGenerator>();
    private readonly IExecutionFlowRegistry _registry = Substitute.For<IExecutionFlowRegistry>();

    public JobDisplayNameResolverTests()
    {
        // Makes names that came from the ID generator easy to recognize in assertions.
        _idGenerator.GenerateId(Arg.Any<Type>()).Returns(ci => "id:" + ci.Arg<Type>().Name);
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>());
        _registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());
    }

    private JobDisplayNameResolver Create() => new JobDisplayNameResolver(_idGenerator, _registry);

    /// <summary>Registers the handler with the display name ExecutionFlowOptions.Add computes: [DisplayName] or the class name.</summary>
    private void RegisterEventHandler<THandler, TEvent>()
    {
        _registry.EventHandlers.Returns(new Dictionary<Type, EventJobRegistryInfo>
        {
            [typeof(TEvent)] = new EventJobRegistryInfo(typeof(THandler), typeof(TEvent), DisplayNameOf(typeof(THandler)))
        });
    }

    private static string DisplayNameOf(Type type) =>
        type.GetCustomAttributes(typeof(DisplayNameAttribute), false).OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName ?? type.Name;

    private static Job EventJob<TEvent>(TEvent @event, string? customName = null)
    {
        var method = typeof(HangfireJobDispatcher).GetMethod(nameof(HangfireJobDispatcher.DispatchEventAsync))!.MakeGenericMethod(typeof(TEvent));
        return new Job(typeof(HangfireJobDispatcher), method, new object?[] { @event, customName, null, CancellationToken.None });
    }

    // --- GetConfiguredName order ---

    [Fact]
    public void GetName_UsesCustomName_First()
    {
        RegisterEventHandler<NamedHandler, NamedEvent>();

        Assert.Equal("Custom", Create().GetName(EventJob(new NamedEvent(), "Custom")));
    }

    [Fact]
    public void GetName_IgnoresEmptyCustomName()
    {
        RegisterEventHandler<NamedHandler, NamedEvent>();

        Assert.Equal("Named handler", Create().GetName(EventJob(new NamedEvent(), "")));
    }

    [Fact]
    public void GetName_UsesHandlerDisplayName_WhenNoCustomName()
    {
        RegisterEventHandler<NamedHandler, NamedEvent>();

        Assert.Equal("Named handler", Create().GetName(EventJob(new NamedEvent())));
    }

    [Fact]
    public void GetName_UsesEventDisplayName_WhenHandlerNotRegistered()
    {
        Assert.Equal("Named event", Create().GetName(EventJob(new NamedEvent())));
    }

    [Fact]
    public void GetName_IgnoresEventDisplayName_WhenHandlerRegistered()
    {
        RegisterEventHandler<UnnamedHandler, NamedEvent>();

        // The handler is registered but names nothing: the event's [DisplayName] only applies without a handler.
        Assert.Equal("id:" + nameof(UnnamedHandler), Create().GetName(EventJob(new NamedEvent())));
    }

    [Fact]
    public void GetName_UsesRecurringHandlerDisplayName_WhenNotRegistered()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(NamedRecurringHandler));

        Assert.Equal("Named recurring", Create().GetName(job));
    }

    [Fact]
    public void GetName_IgnoresHangfireJobDisplayName()
    {
        RegisterEventHandler<JobDisplayNameHandler, PlainEvent>();

        Assert.Equal("id:" + nameof(JobDisplayNameHandler), Create().GetName(EventJob(new PlainEvent())));
    }

    // --- Fallback ---

    [Fact]
    public void GetName_FallsBackToIdGenerator_ForRegisteredHandler()
    {
        RegisterEventHandler<UnnamedHandler, PlainEvent>();

        Assert.Equal("id:" + nameof(UnnamedHandler), Create().GetName(EventJob(new PlainEvent())));
    }

    [Fact]
    public void GetName_FallsBackToIdGenerator_ForEventType_WhenHandlerNotRegistered()
    {
        Assert.Equal("id:" + nameof(PlainEvent), Create().GetName(EventJob(new PlainEvent())));
    }

    [Fact]
    public void GetName_FallsBackToIdGenerator_ForRecurringHandler_WhenNotRegistered()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(UnnamedRecurringHandler));

        Assert.Equal("id:" + nameof(UnnamedRecurringHandler), Create().GetName(job));
    }

    [Fact]
    public void GetName_NativeJob_FallsBackToIdGenerator_WithJobType()
    {
        var job = Job.FromExpression<INativeService>(x => x.Run());

        Assert.Equal("id:" + nameof(INativeService), Create().GetName(job));
    }

    [Fact]
    public void GetName_ReturnsNull_ForNullJob()
    {
        Assert.Null(Create().GetName(null!));
    }

    [Fact]
    public void Constructor_Throws_ForNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new JobDisplayNameResolver(null!, _registry));
        Assert.Throws<ArgumentNullException>(() => new JobDisplayNameResolver(_idGenerator, null!));
    }

    // Test types. Handlers are abstract: only used as types, and kept out of the assembly scans in other tests.

    public interface INativeService
    {
        void Run();
    }

    [DisplayName("Named event")]
    public class NamedEvent { }

    public class PlainEvent { }

    [DisplayName("Named handler")]
    public abstract class NamedHandler : IHandler<NamedEvent>
    {
        public abstract Task HandleAsync(FlowContext<NamedEvent> context, CancellationToken ct);
    }

    public abstract class UnnamedHandler : IHandler<NamedEvent>, IHandler<PlainEvent>
    {
        public abstract Task HandleAsync(FlowContext<NamedEvent> context, CancellationToken ct);
        public abstract Task HandleAsync(FlowContext<PlainEvent> context, CancellationToken ct);
    }

    public abstract class JobDisplayNameHandler : IHandler<PlainEvent>
    {
        [global::Hangfire.JobDisplayName("From attribute {0}")]
        public abstract Task HandleAsync(FlowContext<PlainEvent> context, CancellationToken ct);
    }

    [DisplayName("Named recurring")]
    [ExecutionFlow.Attributes.Recurring("* * * * *")]
    public abstract class NamedRecurringHandler : IHandler
    {
        public abstract Task HandleAsync(FlowContext context, CancellationToken ct);
    }

    [ExecutionFlow.Attributes.Recurring("* * * * *")]
    public abstract class UnnamedRecurringHandler : IHandler
    {
        public abstract Task HandleAsync(FlowContext context, CancellationToken ct);
    }
}
