#pragma warning disable CS0618 // Also covers the obsolete SetJobAutoRun/SetJobTimeZone shortcuts (014 FR-006 routes them to GetPlanOf).
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>
/// 013-dependencies-by-attribute: the automatic plan in Hangfire. <see cref="HangfireSetup.Build"/> touches Hangfire's static
/// filters, so these run alone and remove what they add.
/// </summary>
[Collection(GlobalHangfireStateCollection.Name)]
public class AutomaticPlanHangfireTests : IDisposable
{
    private readonly List<object> _filtersBefore = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
    private readonly JobStorage _storage = Substitute.For<JobStorage>();
    private readonly JobStorageConnection _connection = Substitute.For<JobStorageConnection>();
    private readonly JobStorageTransaction _transaction = Substitute.For<JobStorageTransaction>();
    private readonly Dictionary<string, Dictionary<string, string>> _hashes = new();

    public AutomaticPlanHangfireTests()
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

    private static HangfireSetup Configure(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        return setup;
    }

    // --- FR-001 ---

    [Fact]
    public void Build_RegistersDependent_AsTriggeredByPlan()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(PrerequisiteHandler));
            o.Add(typeof(DependentHandler));
        });
        setup.Build(Substitute.For<IBackgroundJobClient>(), _storage, new FlowEngineJobActivator(setup));

        Assert.True(setup.IsTriggeredByPlan(typeof(DependentHandler)));
        Assert.Equal(Cron.Never(), _hashes["recurring-job:" + typeof(DependentHandler).FullName]["Cron"]);
        Assert.Equal("*/10 * * * *", _hashes["recurring-job:" + typeof(PrerequisiteHandler).FullName]["Cron"]);
    }

    [Fact]
    public void Configure_TakesEnabledFromSetJobAutoRun()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(PrerequisiteHandler));
            o.Add(typeof(DependentHandler));
            o.Add(typeof(IndependentHandler));
            o.SetJobAutoRun<IndependentHandler>(false);
        });

        Assert.False(setup.Options.Plan!.Get(typeof(IndependentHandler)).Enabled);
        Assert.False(RecurringJobResolver.IsAutoRun(typeof(IndependentHandler), setup.Options));
        Assert.True(RecurringJobResolver.IsAutoRun(typeof(PrerequisiteHandler), setup.Options));
    }

    // --- FR-003 ---

    [Fact]
    public void Configure_Throws_WhenPrerequisiteIsTurnedOff()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Configure(o =>
        {
            o.Add(typeof(PrerequisiteHandler));
            o.Add(typeof(DependentHandler));
            o.SetJobAutoRun<PrerequisiteHandler>(false);
        }));

        Assert.Contains(typeof(DependentHandler).FullName!, ex.Message);
    }

    // --- FR-004 ---

    [Fact]
    public void Configure_IgnoresDependsOn_OutsideExplicitPlan()
    {
        var planner = new ExecutionPlanner();
        planner.Add<PrerequisiteHandler>();
        var plan = planner.Build();

        var setup = Configure(o =>
        {
            o.UsePlan(plan);
            o.Add(typeof(DependentWithRunModeHandler));
        });

        Assert.False(setup.Options.Plan!.TryGet(typeof(DependentWithRunModeHandler), out _));
        Assert.False(setup.IsTriggeredByPlan(typeof(DependentWithRunModeHandler)));
        Assert.True(RecurringJobResolver.IsAutoRun(typeof(DependentWithRunModeHandler), setup.Options));
    }

    // Test types (abstract, so assembly scans in other tests don't pick them up)

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("*/10 * * * *")] private abstract class PrerequisiteHandler : TestHandler { }

    [Recurring("*/5 * * * *"), DependsOn(typeof(PrerequisiteHandler))] private abstract class DependentHandler : TestHandler { }

    [Recurring("0 * * * *")] private abstract class IndependentHandler : TestHandler { }

    [Recurring("*/5 * * * *"), DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval, "00:20:00")]
    private abstract class DependentWithRunModeHandler : TestHandler { }
}
