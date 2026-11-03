#pragma warning disable CS0618 // Also covers the obsolete SetJobAutoRun/SetJobTimeZone shortcuts (014 FR-006 routes them to GetPlanOf).
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>
/// 012-interval-and-overlap FR-002: an interval is registered as an exact UTC cron, or as a per-minute ticker. These call
/// <see cref="HangfireSetup.Build"/>, which touches Hangfire's static filters, so they run alone and remove what they add.
/// </summary>
[Collection(GlobalHangfireStateCollection.Name)]
public class IntervalRegistrationTests : IDisposable
{
    private readonly List<object> _filtersBefore = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
    private readonly JobStorage _storage = Substitute.For<JobStorage>();
    private readonly JobStorageConnection _connection = Substitute.For<JobStorageConnection>();
    private readonly JobStorageTransaction _transaction = Substitute.For<JobStorageTransaction>();
    private readonly Dictionary<string, Dictionary<string, string>> _hashes = new();

    public IntervalRegistrationTests()
    {
        _storage.GetConnection().Returns(_connection);
        _connection.CreateWriteTransaction().Returns(_transaction);
        _connection.GetAllItemsFromSet("recurring-jobs").Returns(new HashSet<string>());
        _transaction.When(t => t.SetRangeInHash(Arg.Any<string>(), Arg.Any<IEnumerable<KeyValuePair<string, string>>>()))
            .Do(ci => _hashes[ci.ArgAt<string>(0)] = ci.ArgAt<IEnumerable<KeyValuePair<string, string>>>(1).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    public void Dispose()
    {
        foreach (var instance in GlobalJobFilters.Filters.Select(f => f.Instance).Except(_filtersBefore).ToList())
            GlobalJobFilters.Filters.Remove(instance);
    }

    private void Build(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        setup.Build(Substitute.For<IBackgroundJobClient>(), _storage, new FlowEngineJobActivator(setup));
    }

    private Dictionary<string, string> RecurringHash(Type handlerType) => _hashes["recurring-job:" + handlerType.FullName];

    [Fact]
    public void Build_RegistersExactCron_WhenIntervalDividesTheHour()
    {
        Build(o => o.Add(typeof(FifteenMinutesHandler)));

        Assert.Equal("*/15 * * * *", RecurringHash(typeof(FifteenMinutesHandler))["Cron"]);
    }

    [Fact]
    public void Build_RegistersExactCron_WhenIntervalDividesTheDay()
    {
        Build(o => o.Add(typeof(SixHoursHandler)));

        Assert.Equal("0 */6 * * *", RecurringHash(typeof(SixHoursHandler))["Cron"]);
    }

    [Fact]
    public void Build_RegistersTicker_WhenNoExactCronExists()
    {
        Build(o => o.Add(typeof(NinetyMinutesHandler)));

        Assert.Equal("* * * * *", RecurringHash(typeof(NinetyMinutesHandler))["Cron"]);
    }

    [Fact]
    public void Build_RegistersIntervalInUtc_EvenWithGlobalTimeZone()
    {
        Build(o =>
        {
            o.RecurringTimeZone = "America/Sao_Paulo";
            o.Add(typeof(SixHoursHandler));
        });

        Assert.Equal(TimeZoneInfo.Utc.Id, RecurringHash(typeof(SixHoursHandler))["TimeZoneId"]);
    }

    [Fact]
    public void Build_RegistersNever_WhenIntervalHandlerDoesNotAutoRun()
    {
        Build(o =>
        {
            o.Add(typeof(NinetyMinutesHandler));
            o.SetJobAutoRun<NinetyMinutesHandler>(false);
        });

        Assert.Equal(Cron.Never(), RecurringHash(typeof(NinetyMinutesHandler))["Cron"]);
    }

    [Fact]
    public void Configure_Warns_WhenIntervalHandlerHasSetJobTimeZone()
    {
        var messages = CaptureTrace(() => new HangfireSetup().Configure(o =>
        {
            o.Add(typeof(SixHoursHandler));
            o.SetJobTimeZone<SixHoursHandler>("America/Sao_Paulo");
        }));

        Assert.Contains(messages, m => m.Contains(typeof(SixHoursHandler).FullName!) && m.Contains("America/Sao_Paulo"));
    }

    [Fact]
    public void Configure_Throws_WhenIntervalHandlerHasUnknownSetJobTimeZone()
    {
        Assert.Throws<InvalidOperationException>(() => new HangfireSetup().Configure(o =>
        {
            o.Add(typeof(SixHoursHandler));
            o.SetJobTimeZone<SixHoursHandler>("Not/AZone");
        }));
    }

    [Fact]
    public void Configure_DoesNotWarn_ForGlobalTimeZoneOnIntervalHandler()
    {
        var messages = CaptureTrace(() => new HangfireSetup().Configure(o =>
        {
            o.RecurringTimeZone = "America/Sao_Paulo";
            o.Add(typeof(FifteenMinutesHandler));
        }));

        Assert.DoesNotContain(messages, m => m.Contains(typeof(FifteenMinutesHandler).FullName!));
    }

    private static List<string> CaptureTrace(Action action)
    {
        var listener = new CollectingTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            action();
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        lock (listener.Messages)
            return listener.Messages.ToList();
    }

    private sealed class CollectingTraceListener : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = new();

        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        private void Add(string? message)
        {
            lock (Messages)
                Messages.Add(message ?? string.Empty);
        }
    }

    [Fact]
    public void Configure_Accepts_IntervalAsSchedule()
    {
        var setup = new HangfireSetup();

        setup.Configure(o => o.Add(typeof(FifteenMinutesHandler)));

        Assert.True(setup.RecurringHandlers.ContainsKey(typeof(FifteenMinutesHandler)));
    }

    [Theory]
    [InlineData("00:01:00", "* * * * *")]
    [InlineData("00:05:00", "*/5 * * * *")]
    [InlineData("00:30:00", "*/30 * * * *")]
    [InlineData("01:00:00", "0 * * * *")]
    [InlineData("02:00:00", "0 */2 * * *")]
    [InlineData("12:00:00", "0 */12 * * *")]
    [InlineData("1.00:00:00", "0 0 * * *")]
    public void TryGetExactCron_ReturnsCron_WhenOneExists(string interval, string expected)
    {
        Assert.True(IntervalCron.TryGetExactCron(TimeSpan.Parse(interval), out var cron));
        Assert.Equal(expected, cron);
    }

    [Theory]
    [InlineData("00:07:00")]
    [InlineData("00:45:00")]
    [InlineData("01:30:00")]
    [InlineData("00:01:30")]
    [InlineData("05:00:00")]
    [InlineData("2.00:00:00")]
    public void TryGetExactCron_ReturnsFalse_WhenNoExactCronExists(string interval)
    {
        Assert.False(IntervalCron.TryGetExactCron(TimeSpan.Parse(interval), out _));
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [RecurringInterval("00:15:00")] private abstract class FifteenMinutesHandler : TestHandler { }
    [RecurringInterval("06:00:00")] private abstract class SixHoursHandler : TestHandler { }
    [RecurringInterval("01:30:00")] private abstract class NinetyMinutesHandler : TestHandler { }
}
