using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

/// <summary>
/// 012-interval-and-overlap FR-003 (AC-003.3..AC-003.5): <see cref="NoOverlapFilter"/> holds a distributed lock per recurring
/// job for the whole run, so a second run waits for it, up to the timeout.
/// </summary>
public class OverlapFilterTests
{
    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();
    private readonly IDisposable _lock = Substitute.For<IDisposable>();

    public OverlapFilterTests()
    {
        _connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(_lock);
    }

    private static HangfireSetup CreateSetup(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        return setup;
    }

    private PerformContext Context(Type handlerType)
    {
        var backgroundJob = new BackgroundJob("job-1", JobBuilder.CreateRecurringJob(handlerType), DateTime.UtcNow);
        return new PerformContext(Substitute.For<JobStorage>(), _connection, backgroundJob, Substitute.For<IJobCancellationToken>());
    }

    [Fact]
    public void Perform_Waits_WhenRunInProgress()
    {
        var setup = CreateSetup(o => o.Add(typeof(NoOverlapHandler)));
        var filter = new NoOverlapFilter(setup);
        var context = Context(typeof(NoOverlapHandler));

        filter.OnPerforming(new PerformingContext(context));

        // The lock is per recurring job and waits up to the timeout; it's held until the run ends.
        _connection.Received(1).AcquireDistributedLock(NoOverlapFilter.GetLockResource(typeof(NoOverlapHandler).FullName!), TimeSpan.FromMinutes(10));
        _lock.DidNotReceive().Dispose();

        filter.OnPerformed(new PerformedContext(context, null, false, null));
        _lock.Received(1).Dispose();
    }

    [Fact]
    public void Perform_Fails_WhenTimeoutExpires()
    {
        _connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Throws(new DistributedLockTimeoutException("executionflow:overlap:x"));
        var setup = CreateSetup(o => o.Add(typeof(NoOverlapHandler)));

        Assert.Throws<DistributedLockTimeoutException>(() =>
            new NoOverlapFilter(setup).OnPerforming(new PerformingContext(Context(typeof(NoOverlapHandler)))));
    }

    [Fact]
    public void Perform_AllowsOverlap_ByDefault()
    {
        var setup = CreateSetup(o => o.Add(typeof(OverlappingHandler)));
        var filter = new NoOverlapFilter(setup);
        var context = Context(typeof(OverlappingHandler));

        filter.OnPerforming(new PerformingContext(context));
        filter.OnPerformed(new PerformedContext(context, null, false, null));

        _connection.DidNotReceive().AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Perform_UsesPlanTimeout_OverAttribute()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NoOverlapHandler>().NoOverlap(TimeSpan.FromMinutes(2));
        var plan = planner.Build();
        var setup = CreateSetup(o => o.UsePlan(plan));

        new NoOverlapFilter(setup).OnPerforming(new PerformingContext(Context(typeof(NoOverlapHandler))));

        _connection.Received(1).AcquireDistributedLock(Arg.Any<string>(), TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Perform_Ignores_EventJobs()
    {
        var setup = CreateSetup(o => o.Add(typeof(NoOverlapHandler)));
        var backgroundJob = new BackgroundJob("job-2", JobBuilder.CreateEventJob(new TestEvent()), DateTime.UtcNow);
        var context = new PerformContext(Substitute.For<JobStorage>(), _connection, backgroundJob, Substitute.For<IJobCancellationToken>());

        new NoOverlapFilter(setup).OnPerforming(new PerformingContext(context));

        _connection.DidNotReceive().AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    // Test types

    public class TestEvent { }

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("*/5 * * * *"), NoOverlap(600)] private class NoOverlapHandler : TestHandler { }
    [Recurring("*/5 * * * *")] private class OverlappingHandler : TestHandler { }
}
