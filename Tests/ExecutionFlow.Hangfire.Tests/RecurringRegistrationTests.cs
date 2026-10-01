#pragma warning disable CS0618 // Checks that the obsolete HangfireAutoRunFilter is no longer registered.
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>
/// recurring-jobs REQ-002, REQ-003, REQ-006, REQ-007, REQ-008. These call <see cref="HangfireSetup.Build"/>,
/// which touches Hangfire's static filters, so they run alone and remove what they add.
/// </summary>
[Collection(GlobalHangfireStateCollection.Name)]
public class RecurringRegistrationTests : IDisposable
{
    private readonly List<object> _filtersBefore = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
    private readonly JobStorage _storage = Substitute.For<JobStorage>();
    private readonly JobStorageConnection _connection = Substitute.For<JobStorageConnection>();
    private readonly JobStorageTransaction _transaction = Substitute.For<JobStorageTransaction>();
    private readonly Dictionary<string, Dictionary<string, string>> _hashes = new();
    private readonly List<string> _removedRecurringIds = new();

    public RecurringRegistrationTests()
    {
        _storage.GetConnection().Returns(_connection);
        _connection.CreateWriteTransaction().Returns(_transaction);
        _connection.GetAllItemsFromSet("recurring-jobs").Returns(new HashSet<string>());
        _transaction.When(t => t.SetRangeInHash(Arg.Any<string>(), Arg.Any<IEnumerable<KeyValuePair<string, string>>>()))
            .Do(ci => _hashes[ci.ArgAt<string>(0)] = ci.ArgAt<IEnumerable<KeyValuePair<string, string>>>(1).ToDictionary(kv => kv.Key, kv => kv.Value));
        _transaction.When(t => t.RemoveFromSet("recurring-jobs", Arg.Any<string>()))
            .Do(ci => _removedRecurringIds.Add(ci.ArgAt<string>(1)));
    }

    public void Dispose()
    {
        foreach (var instance in GlobalJobFilters.Filters.Select(f => f.Instance).Except(_filtersBefore).ToList())
            GlobalJobFilters.Filters.Remove(instance);
    }

    private HangfireSetup Build(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        setup.Build(Substitute.For<IBackgroundJobClient>(), _storage, new FlowEngineJobActivator(setup));
        return setup;
    }

    private Dictionary<string, string> RecurringHash(string id) => _hashes["recurring-job:" + id];

    /// <summary>Seeds recurring jobs the way Hangfire stores them: the "recurring-jobs" set plus one hash per job.</summary>
    private void SeedExistingRecurringJobs(params (string Id, Job? Job)[] jobs)
    {
        _connection.GetAllItemsFromSet("recurring-jobs").Returns(new HashSet<string>(jobs.Select(j => j.Id)));
        foreach (var (id, job) in jobs)
        {
            var entries = new Dictionary<string, string> { ["Cron"] = "0 0 * * *" };
            entries["Job"] = job != null
                ? InvocationData.SerializeJob(job).SerializePayload()
                : "{\"Type\":\"Missing.Type, Missing.Assembly\",\"Method\":\"Run\",\"ParameterTypes\":\"[]\"}";
            _connection.GetAllEntriesFromHash("recurring-job:" + id).Returns(entries);
        }
    }

    // --- REQ-002 / REQ-007: IDs ---

    [Fact]
    public void Build_UsesFullNameAsId_WhenNoExplicitId()
    {
        Build(o => o.Add(typeof(DailyHandler)));

        Assert.Equal("0 8 * * *", RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_UsesExplicitId_OverIdGenerator()
    {
        Build(o =>
        {
            o.Add(typeof(ExplicitIdHandler));
            o.SetJobIdGeneratorType<PrefixIdGenerator>();
        });

        Assert.True(_hashes.ContainsKey("recurring-job:stable-report"));
        Assert.DoesNotContain(_hashes.Keys, k => k.StartsWith("recurring-job:custom-"));
    }

    [Fact]
    public void Build_UsesIdGenerator_WhenNoExplicitId()
    {
        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.SetJobIdGeneratorType<PrefixIdGenerator>();
        });

        Assert.True(_hashes.ContainsKey("recurring-job:custom-" + nameof(DailyHandler)));
    }

    [Fact]
    public void Build_Throws_WhenTwoHandlersResolveToTheSameId()
    {
        var setup = new HangfireSetup();
        setup.Configure(o =>
        {
            o.Add(typeof(ExplicitIdHandler));
            o.Add(typeof(SameIdHandler));
        });

        var ex = Assert.Throws<InvalidOperationException>(() => setup.Build(Substitute.For<IBackgroundJobClient>(), _storage, new FlowEngineJobActivator(setup)));
        Assert.Contains("stable-report", ex.Message);
    }

    [Fact]
    public void Trigger_UsesExplicitId()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => o.Add(typeof(ExplicitIdHandler)));
        var dispatcher = setup.Build(Substitute.For<IBackgroundJobClient>(), _storage, new FlowEngineJobActivator(setup));
        _connection.ClearReceivedCalls();

        try { dispatcher.Trigger(typeof(ExplicitIdHandler)); } catch { /* the mocked storage has no job data */ }

        _connection.Received().GetAllEntriesFromHash("recurring-job:stable-report");
    }

    // --- REQ-008: time zone precedence (option > attribute > global > UTC) ---

    [Fact]
    public void Build_UsesUtc_ByDefault()
    {
        Build(o => o.Add(typeof(DailyHandler)));

        Assert.Equal(TimeZoneInfo.Utc.Id, RecurringHash(typeof(DailyHandler).FullName!)["TimeZoneId"]);
    }

    [Fact]
    public void Build_UsesGlobalTimeZone_WhenNothingMoreSpecific()
    {
        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.RecurringTimeZone = "America/Sao_Paulo";
        });

        Assert.Equal("America/Sao_Paulo", RecurringHash(typeof(DailyHandler).FullName!)["TimeZoneId"]);
    }

    [Fact]
    public void Build_UsesAttributeTimeZone_OverGlobal()
    {
        Build(o =>
        {
            o.Add(typeof(NewYorkHandler));
            o.RecurringTimeZone = "America/Sao_Paulo";
        });

        Assert.Equal("America/New_York", RecurringHash(typeof(NewYorkHandler).FullName!)["TimeZoneId"]);
    }

    [Fact]
    public void Build_UsesOptionTimeZone_OverAttribute()
    {
        Build(o =>
        {
            o.Add(typeof(NewYorkHandler));
            o.SetJobTimeZone<NewYorkHandler>("Europe/Lisbon");
        });

        Assert.Equal("Europe/Lisbon", RecurringHash(typeof(NewYorkHandler).FullName!)["TimeZoneId"]);
    }

    // --- REQ-003: auto-run off = Cron.Never, no auto-run filter ---

    [Fact]
    public void Build_RegistersCronNever_WhenAutoRunIsOff()
    {
        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.SetJobAutoRun<DailyHandler>(false);
        });

        Assert.Equal(Cron.Never(), RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_RegistersDependentWithCronNever()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NewYorkHandler>();
        planner.Add<DailyHandler>().DependsOn<NewYorkHandler>();
        var plan = planner.Build();

        Build(o => o.UsePlan(plan));

        Assert.Equal(Cron.Never(), RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
        Assert.Equal("0 8 * * *", RecurringHash(typeof(NewYorkHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_RegistersOwnScheduleDependentWithItsCron()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NewYorkHandler>();
        planner.Add<DailyHandler>().DependsOn<NewYorkHandler>().RunOnOwnSchedule();
        var plan = planner.Build();

        Build(o => o.UsePlan(plan));

        Assert.Equal("0 8 * * *", RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_RegistersCronNever_WhenGlobalAutoRunIsOff()
    {
        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.GlobalRecurringAutoRun = false;
        });

        Assert.Equal(Cron.Never(), RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_PerHandlerAutoRun_OverridesGlobal()
    {
        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.GlobalRecurringAutoRun = false;
            o.SetJobAutoRun<DailyHandler>(true);
        });

        Assert.Equal("0 8 * * *", RecurringHash(typeof(DailyHandler).FullName!)["Cron"]);
    }

    [Fact]
    public void Build_DoesNotRegisterAutoRunFilter()
    {
        Build(o => o.Add(typeof(DailyHandler)));

        var added = GlobalJobFilters.Filters.Select(f => f.Instance).Except(_filtersBefore);
        Assert.Empty(added.OfType<HangfireAutoRunFilter>());
    }

    // --- REQ-006: orphan cleanup only touches ExecutionFlow recurring jobs ---

    [Fact]
    public void Build_RemovesOnlyExecutionFlowOrphans()
    {
        var executionFlowJob = Job.FromExpression<HangfireJobDispatcher>(x => x.DispatchRecurringAsync(null!, typeof(DailyHandler), default));
        var nativeJob = Job.FromExpression(() => NativeRecurringJob());
        SeedExistingRecurringJobs(
            ("old-executionflow-job", executionFlowJob),
            ("native-hangfire-job", nativeJob),
            ("unloadable-job", null));

        Build(o =>
        {
            o.Add(typeof(DailyHandler));
            o.RemoveOrphanRecurringJobs = true;
        });

        Assert.Equal(new[] { "old-executionflow-job" }, _removedRecurringIds);
    }

    [Fact]
    public void Build_KeepsOrphans_WhenOptionIsOff()
    {
        SeedExistingRecurringJobs(
            ("old-executionflow-job", Job.FromExpression<HangfireJobDispatcher>(x => x.DispatchRecurringAsync(null!, typeof(DailyHandler), default))));

        Build(o => o.Add(typeof(DailyHandler)));

        Assert.Empty(_removedRecurringIds);
    }

    public static void NativeRecurringJob() { }

    // Test types

    [Recurring("0 8 * * *")]
    public class DailyHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 8 * * *", Id = "stable-report")]
    public class ExplicitIdHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 9 * * *", Id = "stable-report")]
    public class SameIdHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 8 * * *", TimeZone = "America/New_York")]
    public class NewYorkHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    public class PrefixIdGenerator : IJobIdGenerator
    {
        public string GenerateId(Type type) => "custom-" + type.Name;
    }
}
