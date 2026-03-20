using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

public class AutoRunFilterTests
{
    private static HangfireAutoRunFilter CreateFilter(bool autoRun, Dictionary<Type, bool> perJob)
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var recurringHandlers = new Dictionary<Type, RecurringJobRegistryInfo>
        {
            { typeof(TestRecurringHandler), new RecurringJobRegistryInfo(typeof(TestRecurringHandler), "Test", null) }
        };
        registry.RecurringHandlers.Returns((IReadOnlyDictionary<Type, RecurringJobRegistryInfo>)recurringHandlers);
        return new HangfireAutoRunFilter(registry, autoRun, perJob);
    }

    private static ElectStateContext CreateContext(
        IState candidateState,
        Job? job,
        bool manuallyTriggered = false)
    {
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();

        var parametersSnapshot = manuallyTriggered
            ? new Dictionary<string, string> { { "Triggered", "1" } }
            : new Dictionary<string, string>();

        var backgroundJob = new BackgroundJob("test-job-1", job, DateTime.UtcNow, parametersSnapshot);

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, candidateState, null);

        return new ElectStateContext(applyContext);
    }

    [Fact]
    public void GlobalFalse_PerJobTrue_Allows()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalFalse_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<AutoStartNotAllowedCanceledState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<AutoStartNotAllowedCanceledState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobTrue_Allows()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_NoPerJobSetting_Allows()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void NonRecurringJob_NotBlocked_EvenWhenGlobalFalse()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateEventJob(new TestEvent()));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    [Fact]
    public void NonEnqueuedState_NotBlocked()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new ScheduledState(TimeSpan.FromMinutes(1)), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ScheduledState>(context.CandidateState);
    }

    [Fact]
    public void ManuallyTriggered_NotBlocked()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)), manuallyTriggered: true);

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    public class TestEvent { }

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
