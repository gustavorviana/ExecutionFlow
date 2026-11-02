using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Client;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

/// <summary>012-interval-and-overlap FR-002 (AC-002.4, AC-002.5) and SC-002: the ticker of an interval with no exact cron.</summary>
public class IntervalTickerTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 10, 0, 3, DateTimeKind.Utc);

    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();
    private readonly Dictionary<string, Dictionary<string, string>> _hashes = new();
    private DateTime _now = Start;

    public IntervalTickerTests()
    {
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        transaction.When(t => t.SetRangeInHash(Arg.Any<string>(), Arg.Any<IEnumerable<KeyValuePair<string, string>>>()))
            .Do(ci =>
            {
                if (!_hashes.TryGetValue(ci.ArgAt<string>(0), out var hash))
                    _hashes[ci.ArgAt<string>(0)] = hash = new Dictionary<string, string>();
                foreach (var pair in ci.ArgAt<IEnumerable<KeyValuePair<string, string>>>(1))
                    hash[pair.Key] = pair.Value;
            });

        _connection.CreateWriteTransaction().Returns(transaction);
        _connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(Substitute.For<IDisposable>());
        _connection.GetAllEntriesFromHash(Arg.Any<string>())
            .Returns(ci => _hashes.TryGetValue(ci.ArgAt<string>(0), out var hash) ? new Dictionary<string, string>(hash) : null);
    }

    private HangfireSetup CreateSetup(params Type[] handlerTypes)
    {
        var setup = new HangfireSetup();
        setup.Configure(o =>
        {
            foreach (var type in handlerTypes)
                o.Add(type);
        });
        setup.UtcNow = () => _now;
        return setup;
    }

    /// <summary>Runs the filter for the creation of a job, as the scheduler (or a manual trigger) would create it.</summary>
    private CreatingContext Create(HangfireSetup setup, Type handlerType, bool bySchedule = true)
    {
        var state = new EnqueuedState { Reason = bySchedule ? PrerequisiteGateFilter.SchedulerEnqueueReason : "Triggered using recurring job manager" };
        var context = new CreatingContext(new CreateContext(Substitute.For<JobStorage>(), _connection, JobBuilder.CreateRecurringJob(handlerType), state));

        new IntervalTickerFilter(setup).OnCreating(context);
        return context;
    }

    [Fact]
    public void OnCreating_Creates_WhenIntervalElapsed()
    {
        var setup = CreateSetup(typeof(NinetyMinutesHandler));

        Assert.False(Create(setup, typeof(NinetyMinutesHandler)).Canceled);

        _now = Start.AddMinutes(90);
        Assert.False(Create(setup, typeof(NinetyMinutesHandler)).Canceled);
    }

    [Fact]
    public void OnCreating_Cancels_WhenIntervalNotElapsed()
    {
        var setup = CreateSetup(typeof(NinetyMinutesHandler));
        Create(setup, typeof(NinetyMinutesHandler));
        var writesBefore = _hashes[IntervalTickerStore.LastRunKey][typeof(NinetyMinutesHandler).FullName!];

        _now = Start.AddMinutes(89);
        var context = Create(setup, typeof(NinetyMinutesHandler));

        // Canceled at creation: Hangfire stores no job, and the last run isn't moved.
        Assert.True(context.Canceled);
        Assert.Equal(writesBefore, _hashes[IntervalTickerStore.LastRunKey][typeof(NinetyMinutesHandler).FullName!]);
    }

    [Fact]
    public void OnCreating_KeepsCadence_WhenSchedulerFiresSecondsLate()
    {
        var setup = CreateSetup(typeof(SevenMinutesHandler));
        Create(setup, typeof(SevenMinutesHandler)); // 10:00:03

        _now = new DateTime(2026, 1, 1, 10, 7, 1, DateTimeKind.Utc);

        Assert.False(Create(setup, typeof(SevenMinutesHandler)).Canceled);
    }

    [Fact]
    public void Trigger_Runs_WhenIntervalNotElapsed()
    {
        var setup = CreateSetup(typeof(NinetyMinutesHandler));
        Create(setup, typeof(NinetyMinutesHandler));

        _now = Start.AddMinutes(10);
        Assert.False(Create(setup, typeof(NinetyMinutesHandler), bySchedule: false).Canceled);

        // The manual run counts as the last run.
        _now = Start.AddMinutes(95);
        Assert.True(Create(setup, typeof(NinetyMinutesHandler)).Canceled);
    }

    [Fact]
    public void OnCreating_Ignores_IntervalWithExactCron()
    {
        var setup = CreateSetup(typeof(FifteenMinutesHandler));

        Create(setup, typeof(FifteenMinutesHandler));
        var second = Create(setup, typeof(FifteenMinutesHandler));

        Assert.False(second.Canceled);
        Assert.False(_hashes.ContainsKey(IntervalTickerStore.LastRunKey));
    }

    [Fact]
    public void OnCreating_Ignores_CronHandler()
    {
        var setup = CreateSetup(typeof(CronHandler));

        Assert.False(Create(setup, typeof(CronHandler)).Canceled);
        Assert.False(_hashes.ContainsKey(IntervalTickerStore.LastRunKey));
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [RecurringInterval("01:30:00")] private abstract class NinetyMinutesHandler : TestHandler { }
    [RecurringInterval("00:07:00")] private abstract class SevenMinutesHandler : TestHandler { }
    [RecurringInterval("00:15:00")] private abstract class FifteenMinutesHandler : TestHandler { }
    [Recurring("*/5 * * * *")] private abstract class CronHandler : TestHandler { }
}
