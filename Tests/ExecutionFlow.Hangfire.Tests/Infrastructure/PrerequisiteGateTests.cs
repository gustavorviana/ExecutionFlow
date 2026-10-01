using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

/// <summary>recurring-jobs REQ-010 and REQ-012: the prerequisite gate, triggers and run modes.</summary>
public class PrerequisiteGateTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();
    private readonly Dictionary<string, Dictionary<string, string>> _hashes = new();
    private readonly Dictionary<string, string> _jobStates = new();
    private readonly List<string> _triggered = new();
    private readonly List<(string Id, TimeSpan Delay)> _scheduled = new();
    private DateTime _now = Start;
    private int _jobCounter;

    public PrerequisiteGateTests()
    {
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        transaction.When(t => t.SetRangeInHash(Arg.Any<string>(), Arg.Any<IEnumerable<KeyValuePair<string, string>>>()))
            .Do(ci =>
            {
                var hash = Hash(ci.ArgAt<string>(0));
                foreach (var pair in ci.ArgAt<IEnumerable<KeyValuePair<string, string>>>(1))
                    hash[pair.Key] = pair.Value;
            });

        _connection.CreateWriteTransaction().Returns(transaction);
        _connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(Substitute.For<IDisposable>());
        _connection.GetAllEntriesFromHash(Arg.Any<string>())
            .Returns(ci => _hashes.TryGetValue(ci.ArgAt<string>(0), out var hash) ? new Dictionary<string, string>(hash) : null);
        _connection.GetStateData(Arg.Any<string>())
            .Returns(ci => _jobStates.TryGetValue(ci.ArgAt<string>(0), out var state) ? new StateData { Name = state } : null);
    }

    private Dictionary<string, string> Hash(string key)
    {
        if (!_hashes.TryGetValue(key, out var hash))
            _hashes[key] = hash = new Dictionary<string, string>();
        return hash;
    }

    private HangfireSetup CreateSetup(Action<ExecutionPlanner> declare)
    {
        var planner = new ExecutionPlanner();
        declare(planner);
        var plan = planner.Build();

        var setup = new HangfireSetup();
        setup.Configure(o => o.UsePlan(plan));
        setup.TriggerRecurringJob = (_, id) => _triggered.Add(id);
        setup.ScheduleDependentTrigger = (_, id, delay) => _scheduled.Add((id, delay));
        setup.UtcNow = () => _now;
        return setup;
    }

    private HangfireSetup CreateChainSetup(Action<ExecutionPlanEntry>? configureDependent = null)
    {
        return CreateSetup(p =>
        {
            p.Add<PrerequisiteHandler>();
            var dependent = p.Add<DependentHandler>().DependsOn<PrerequisiteHandler>();
            configureDependent?.Invoke(dependent);
        });
    }

    private static string Id<T>() => typeof(T).FullName!;

    /// <summary>Elects the creation of a job; <paramref name="reason"/> is the enqueue reason Hangfire passes.</summary>
    private ElectStateContext Elect(HangfireSetup setup, Type handlerType, string? reason = null, string? currentState = null)
    {
        var jobId = "job-" + ++_jobCounter;
        var backgroundJob = new BackgroundJob(jobId, JobBuilder.CreateRecurringJob(handlerType), DateTime.UtcNow);
        var applyContext = new ApplyStateContext(
            Substitute.For<JobStorage>(), _connection, Substitute.For<IWriteOnlyTransaction>(), backgroundJob, new EnqueuedState { Reason = reason }, currentState);
        var context = new ElectStateContext(applyContext);

        new PrerequisiteGateFilter(setup).OnStateElection(context);

        if (context.CandidateState is EnqueuedState)
            _jobStates[jobId] = ProcessingState.StateName;

        return context;
    }

    /// <summary>A job the plan triggered: the trigger marker is set before the job is created, as DecideTrigger does.</summary>
    private ElectStateContext ElectPlanTrigger(HangfireSetup setup, Type handlerType)
    {
        Hash(PrerequisiteGenerationStore.GetStateKey(handlerType.FullName!))[PrerequisiteGenerationStore.TriggeredField] = "1";
        return Elect(setup, handlerType);
    }

    private void RunHandler(HangfireSetup setup, Type handlerType)
    {
        var activator = new FlowEngineJobActivator(setup);
        activator.RegisterLoggerFactory(setup.LoggerFactoryTypes);
        var backgroundJob = new BackgroundJob("run-" + ++_jobCounter, JobBuilder.CreateRecurringJob(handlerType), DateTime.UtcNow);
        var performContext = new PerformContext(Substitute.For<JobStorage>(), _connection, backgroundJob, Substitute.For<IJobCancellationToken>());

        new HangfireJobDispatcher(activator, setup).DispatchRecurringAsync(performContext, handlerType, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Marks the dependent's active job as finished, as the Succeeded transition would.</summary>
    private void FinishActiveJobs()
    {
        foreach (var key in _jobStates.Keys.ToList())
            _jobStates[key] = SucceededState.StateName;
    }

    private string Generation<T>() =>
        _hashes.TryGetValue(PrerequisiteGenerationStore.GenerationKey, out var hash) && hash.TryGetValue(Id<T>(), out var value) ? value : "0";

    // --- REQ-010: generations and the gate ---

    [Fact]
    public void DispatchRecurring_IncrementsGeneration_WhenPrerequisiteCompletes()
    {
        var setup = CreateChainSetup();

        RunHandler(setup, typeof(PrerequisiteHandler));
        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Equal("2", Generation<PrerequisiteHandler>());
    }

    [Fact]
    public void DispatchRecurring_DoesNotIncrement_WhenHandlerThrows()
    {
        var setup = CreateSetup(p =>
        {
            p.Add<ThrowingHandler>();
            p.Add<DependentHandler>().DependsOn<ThrowingHandler>();
        });

        Assert.Throws<InvalidOperationException>(() => RunHandler(setup, typeof(ThrowingHandler)));

        Assert.Equal("0", Generation<ThrowingHandler>());
        Assert.Empty(_triggered);
    }

    [Fact]
    public void OnStateElection_ElectsPrerequisitesNotMet_WhenNoNewGeneration()
    {
        var context = ElectPlanTrigger(CreateChainSetup(), typeof(DependentHandler));

        var state = Assert.IsType<PrerequisitesNotMetState>(context.CandidateState);
        Assert.Contains(Id<PrerequisiteHandler>(), state.Reason);
        Assert.True(state.IsFinal);
    }

    [Fact]
    public void OnStateElection_EnqueuesOncePerGeneration()
    {
        var setup = CreateChainSetup();
        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.IsType<EnqueuedState>(ElectPlanTrigger(setup, typeof(DependentHandler)).CandidateState);
        Assert.IsType<PrerequisitesNotMetState>(ElectPlanTrigger(setup, typeof(DependentHandler)).CandidateState);
    }

    [Fact]
    public void OnStateElection_DoesNotGate_WhenJobIsNotNew()
    {
        var context = Elect(CreateChainSetup(), typeof(DependentHandler), currentState: ScheduledState.StateName);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void OnStateElection_DoesNotGate_WhenHandlerHasNoPrerequisites()
    {
        var setup = CreateChainSetup();

        Assert.IsType<EnqueuedState>(Elect(setup, typeof(PrerequisiteHandler)).CandidateState);
    }

    // --- REQ-010: triggers ---

    [Fact]
    public void DispatchRecurring_TriggersDependent_WhenAllPrerequisitesHaveNewCycle()
    {
        var setup = CreateChainSetup();

        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Equal(new[] { Id<DependentHandler>() }, _triggered);
    }

    [Fact]
    public void DispatchRecurring_DoesNotTrigger_WhenAnotherPrerequisiteIsPending()
    {
        var setup = CreateSetup(p =>
        {
            p.Add<PrerequisiteHandler>();
            p.Add<OtherPrerequisiteHandler>();
            p.Add<DependentHandler>().DependsOn<PrerequisiteHandler>().DependsOn<OtherPrerequisiteHandler>();
        });

        RunHandler(setup, typeof(PrerequisiteHandler));
        Assert.Empty(_triggered);

        RunHandler(setup, typeof(OtherPrerequisiteHandler));
        Assert.Equal(new[] { Id<DependentHandler>() }, _triggered);
    }

    [Fact]
    public void DispatchRecurring_DoesNotTrigger_DisabledDependent()
    {
        var setup = CreateChainSetup(d => d.Enabled(false));

        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Empty(_triggered);
    }

    [Fact]
    public void DispatchRecurring_TriggersChainInOrder_WhenDependentAlsoDependsOnRoot()
    {
        // C → B → A and C → A: C triggers only B; B triggers A.
        var setup = CreateSetup(p =>
        {
            p.Add<PrerequisiteHandler>();                                                                   // C
            p.Add<OtherPrerequisiteHandler>().DependsOn<PrerequisiteHandler>();                             // B
            p.Add<DependentHandler>().DependsOn<OtherPrerequisiteHandler>().DependsOn<PrerequisiteHandler>(); // A
        });

        RunHandler(setup, typeof(PrerequisiteHandler));
        Assert.Equal(new[] { Id<OtherPrerequisiteHandler>() }, _triggered);

        Elect(setup, typeof(OtherPrerequisiteHandler));   // B's triggered job is created...
        FinishActiveJobs();
        RunHandler(setup, typeof(OtherPrerequisiteHandler)); // ...and completes.
        Assert.Equal(new[] { Id<OtherPrerequisiteHandler>(), Id<DependentHandler>() }, _triggered);
    }

    // --- REQ-012: run modes and manual runs ---

    [Fact]
    public void DispatchRecurring_DoesNotTrigger_OwnScheduleDependent()
    {
        var setup = CreateChainSetup(d => d.RunOnOwnSchedule());

        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Empty(_triggered);
    }

    [Fact]
    public void OnStateElection_GatesSchedulerOccurrence_OfOwnScheduleDependent()
    {
        var setup = CreateChainSetup(d => d.RunOnOwnSchedule());

        var withoutCycle = Elect(setup, typeof(DependentHandler), PrerequisiteGateFilter.SchedulerEnqueueReason);
        RunHandler(setup, typeof(PrerequisiteHandler));
        var withCycle = Elect(setup, typeof(DependentHandler), PrerequisiteGateFilter.SchedulerEnqueueReason);

        Assert.IsType<PrerequisitesNotMetState>(withoutCycle.CandidateState);
        Assert.IsType<EnqueuedState>(withCycle.CandidateState);
    }

    [Fact]
    public void OnStateElection_RunsManualTrigger_WithoutGate()
    {
        var setup = CreateChainSetup();
        RunHandler(setup, typeof(PrerequisiteHandler));
        _triggered.Clear();

        var manual = Elect(setup, typeof(DependentHandler), "Triggered via Dashboard UI");
        var planAfterManual = ElectPlanTrigger(setup, typeof(DependentHandler));

        Assert.IsType<EnqueuedState>(manual.CandidateState);
        // The manual run consumed the prerequisite's cycle.
        Assert.IsType<PrerequisitesNotMetState>(planAfterManual.CandidateState);
    }

    [Fact]
    public void OnStateElection_RunsManualTrigger_WhenPrerequisiteNeverRan()
    {
        var context = Elect(CreateChainSetup(), typeof(DependentHandler), "Triggered via Dashboard UI");

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void DispatchRecurring_TriggersNow_WhenMinIntervalElapsed()
    {
        var setup = CreateChainSetup(d => d.MinInterval(TimeSpan.FromMinutes(10)));

        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Equal(new[] { Id<DependentHandler>() }, _triggered);
        Assert.Empty(_scheduled);
    }

    [Fact]
    public void DispatchRecurring_SchedulesTrigger_WhenMinIntervalNotElapsed()
    {
        var setup = CreateChainSetup(d => d.MinInterval(TimeSpan.FromMinutes(10)));
        RunHandler(setup, typeof(PrerequisiteHandler));
        ElectPlanTrigger(setup, typeof(DependentHandler));
        FinishActiveJobs();
        RunHandler(setup, typeof(DependentHandler));        // finishes at 00:00
        _triggered.Clear();

        _now = Start.AddMinutes(4);
        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Empty(_triggered);
        Assert.Equal(new[] { (Id<DependentHandler>(), TimeSpan.FromMinutes(6)) }, _scheduled);
    }

    [Fact]
    public void DispatchRecurring_SchedulesOnlyOneTrigger_PerDependent()
    {
        var setup = CreateChainSetup(d => d.MinInterval(TimeSpan.FromMinutes(10)));
        RunHandler(setup, typeof(PrerequisiteHandler));
        ElectPlanTrigger(setup, typeof(DependentHandler));
        FinishActiveJobs();
        RunHandler(setup, typeof(DependentHandler));

        _now = Start.AddMinutes(2);
        RunHandler(setup, typeof(PrerequisiteHandler));
        _now = Start.AddMinutes(5);
        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Single(_scheduled);
    }

    [Fact]
    public void TriggerPlanDependent_TriggersDependent_WhenStillReady()
    {
        var setup = CreateChainSetup(d => d.MinInterval(TimeSpan.FromMinutes(10)));
        RunHandler(setup, typeof(PrerequisiteHandler));
        ElectPlanTrigger(setup, typeof(DependentHandler));
        FinishActiveJobs();
        RunHandler(setup, typeof(DependentHandler));
        _now = Start.AddMinutes(4);
        RunHandler(setup, typeof(PrerequisiteHandler));
        _triggered.Clear();

        _now = Start.AddMinutes(10);
        var performContext = new PerformContext(Substitute.For<JobStorage>(), _connection,
            new BackgroundJob("delayed", JobBuilder.CreateRecurringJob(typeof(DependentHandler)), DateTime.UtcNow), Substitute.For<IJobCancellationToken>());
        new HangfireJobDispatcher(new FlowEngineJobActivator(setup), setup).TriggerPlanDependent(performContext, Id<DependentHandler>());

        Assert.Equal(new[] { Id<DependentHandler>() }, _triggered);
    }

    [Fact]
    public void DispatchRecurring_DoesNotTrigger_WhenDependentIsRunning()
    {
        var setup = CreateChainSetup();
        RunHandler(setup, typeof(PrerequisiteHandler));
        ElectPlanTrigger(setup, typeof(DependentHandler));   // the dependent's job is now Processing
        _triggered.Clear();

        RunHandler(setup, typeof(PrerequisiteHandler));

        Assert.Empty(_triggered);
    }

    [Fact]
    public void DispatchRecurring_RetriggersDependent_WhenItFinishesReady()
    {
        var setup = CreateChainSetup();
        RunHandler(setup, typeof(PrerequisiteHandler));
        ElectPlanTrigger(setup, typeof(DependentHandler));
        RunHandler(setup, typeof(PrerequisiteHandler));     // new cycle while the dependent runs
        _triggered.Clear();

        FinishActiveJobs();
        RunHandler(setup, typeof(DependentHandler));

        Assert.Equal(new[] { Id<DependentHandler>() }, _triggered);
    }

    [Recurring("* * * * *")]
    public class PrerequisiteHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("* * * * *")]
    public class OtherPrerequisiteHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("* * * * *")]
    public class DependentHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("* * * * *")]
    public class ThrowingHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }
}
