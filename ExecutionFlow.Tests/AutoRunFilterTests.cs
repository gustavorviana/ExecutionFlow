using ExecutionFlow.Hangfire.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;
using System.Reflection;

namespace ExecutionFlow.Tests;

public class AutoRunFilterTests
{
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
        var method = typeof(ExecutionFlow.Hangfire.Dispatcher.HangfireJobDispatcher)
            .GetMethod(nameof(ExecutionFlow.Hangfire.Dispatcher.HangfireJobDispatcher.DispatchRecurringAsync))!;

        return new Job(
            typeof(ExecutionFlow.Hangfire.Dispatcher.HangfireJobDispatcher),
            method,
            new object[] { "DisplayName", null!, handlerType?.AssemblyQualifiedName ?? "Unknown", CancellationToken.None });
    }

    [Fact]
    public void GlobalFalse_PerJobTrue_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = new HangfireAutoRunFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalFalse_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = new HangfireAutoRunFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobFalse_Blocks()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), false } };
        var filter = new HangfireAutoRunFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<DeletedState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_PerJobTrue_Allows()
    {
        var perJob = new Dictionary<Type, bool> { { typeof(TestRecurringHandler), true } };
        var filter = new HangfireAutoRunFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void GlobalTrue_NoPerJobSetting_Allows()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = new HangfireAutoRunFilter(true, perJob);
        var context = CreateContext(CreateProcessingState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void NonRecurringJob_NotBlocked_EvenWhenGlobalFalse()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = new HangfireAutoRunFilter(false, perJob);
        var context = CreateContext(CreateProcessingState(), null, CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<ProcessingState>(context.CandidateState);
    }

    [Fact]
    public void NonProcessingState_NotBlocked()
    {
        var perJob = new Dictionary<Type, bool>();
        var filter = new HangfireAutoRunFilter(false, perJob);
        var context = CreateContext(new EnqueuedState(), "recurring-1", CreateJobWithHandlerArg(typeof(TestRecurringHandler)));

        filter.OnStateElection(context);

        Assert.IsType<EnqueuedState>(context.CandidateState);
    }

    public class TestRecurringHandler : ExecutionFlow.Abstractions.IHandler
    {
        public Task HandleAsync(Abstractions.ExecutionContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
