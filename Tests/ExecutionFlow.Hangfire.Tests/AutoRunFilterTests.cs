using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;
using System.Reflection;
using HangfireJobDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher;

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

    private static ProcessingState CreateProcessingState()
    {
        return (ProcessingState)Activator.CreateInstance(
            typeof(ProcessingState),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { "server1", "worker1" },
            null)!;
    }

    private static ElectStateContext CreateContext(
        IState candidateState,
        string? recurringJobId,
        Job? job,
        string? currentState = null)
    {
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();
        var backgroundJob = new BackgroundJob("test-job-1", job, DateTime.UtcNow);

        connection.GetJobParameter(backgroundJob.Id, "RecurringJobId")
            .Returns(recurringJobId);

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, candidateState, currentState);

        return new ElectStateContext(applyContext);
    }

    private static Job CreateJobWithHandlerArg(Type? handlerType = null)
    {
        var method = typeof(HangfireJobDispatcher)
            .GetMethod(nameof(HangfireJobDispatcher.DispatchRecurringAsync))!;

        return new Job(
            typeof(HangfireJobDispatcher),
            method,
            new object[] { null!, handlerType!, CancellationToken.None });
    }

    [Fact]
    public void GlobalFalse_PerJobTrue_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalFalse_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobTrue_Allows()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_NoPerJobSetting_Allows()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void NonRecurringJob_NotBlocked_EvenWhenGlobalFalse()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), null, CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void NonProcessingState_NotBlocked()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = CreateFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
