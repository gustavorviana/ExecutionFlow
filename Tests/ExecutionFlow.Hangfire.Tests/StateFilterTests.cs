using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Filters;
using HangfireJobDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;
using System.Reflection;

namespace ExecutionFlow.Hangfire.Tests;

public class StateFilterTests
{
    private readonly IOnEnqueued _onEnqueued = Substitute.For<IOnEnqueued>();
    private readonly IOnProcessing _onProcessing = Substitute.For<IOnProcessing>();
    private readonly IOnSucceeded _onSucceeded = Substitute.For<IOnSucceeded>();
    private readonly IOnFailed _onFailed = Substitute.For<IOnFailed>();
    private readonly IOnCancelled _onCancelled = Substitute.For<IOnCancelled>();
    private readonly IOnRetrying _onRetrying = Substitute.For<IOnRetrying>();

    private HangfireStateFilter CreateFilter()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(IOnEnqueued)).Returns(_onEnqueued);
        serviceProvider.GetService(typeof(IOnProcessing)).Returns(_onProcessing);
        serviceProvider.GetService(typeof(IOnSucceeded)).Returns(_onSucceeded);
        serviceProvider.GetService(typeof(IOnFailed)).Returns(_onFailed);
        serviceProvider.GetService(typeof(IOnCancelled)).Returns(_onCancelled);
        serviceProvider.GetService(typeof(IOnRetrying)).Returns(_onRetrying);

        var stateHandlerTypes = new List<Type>
        {
            typeof(IOnEnqueued), typeof(IOnProcessing), typeof(IOnSucceeded),
            typeof(IOnFailed), typeof(IOnCancelled), typeof(IOnRetrying)
        };

        return new HangfireStateFilter(registry, serviceProvider, stateHandlerTypes);
    }

    private static ElectStateContext CreateContext(
        IState candidateState,
        string? currentState = null,
        string? customId = null,
        Job? job = null)
    {
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();
        var bgJob = job ?? CreateTestJob();
        var backgroundJob = new BackgroundJob("test-job-1", bgJob, DateTime.UtcNow);

        if (customId != null)
        {
            connection.GetJobParameter(backgroundJob.Id, Infrastructure.HangfireDispatcher.EventId)
                .Returns(customId);
        }

        connection.GetJobParameter(backgroundJob.Id, "RetryCount")
            .Returns((string?)null);

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, candidateState, currentState);

        return new ElectStateContext(applyContext);
    }

    private static Job CreateTestJob()
    {
        var method = typeof(HangfireJobDispatcher)
            .GetMethod(nameof(HangfireJobDispatcher.DispatchRecurringAsync))!;

        return new Job(
            typeof(HangfireJobDispatcher),
            method,
            new object[] { null!, typeof(TestHandler)!, CancellationToken.None });
    }

    [Fact]
    public void EnqueuedState_Calls_OnEnqueued()
    {
        var filter = CreateFilter();
        var context = CreateContext(new EnqueuedState());

        filter.OnStateElection(context);

        _onEnqueued.Received(1).OnEnqueued(Arg.Is<ExecutionEvent>(e =>
            e.JobId == "test-job-1"));
    }

    [Fact]
    public void ProcessingState_Calls_OnProcessing()
    {
        var filter = CreateFilter();
        var processingState = CreateProcessingState();
        var context = CreateContext(processingState);

        filter.OnStateElection(context);

        _onProcessing.Received(1).OnProcessing(Arg.Is<ExecutionEvent>(e =>
            e.JobId == "test-job-1"));
    }

    [Fact]
    public void SucceededState_Calls_OnSucceeded_WithDuration()
    {
        var filter = CreateFilter();
        var succeededState = new SucceededState(null, 100, 500);
        var context = CreateContext(succeededState);

        filter.OnStateElection(context);

        _onSucceeded.Received(1).OnSucceeded(Arg.Is<ExecutionSucceededEvent>(e =>
            e.JobId == "test-job-1"));
    }

    [Fact]
    public void FailedState_Calls_OnFailed_WithException()
    {
        var filter = CreateFilter();
        var exception = new InvalidOperationException("test error");
        var failedState = new FailedState(exception);
        var context = CreateContext(failedState);

        filter.OnStateElection(context);

        _onFailed.Received(1).OnFailed(Arg.Is<ExecutionFailedEvent>(e =>
            e.JobId == "test-job-1" && e.Exception == exception));
    }

    [Fact]
    public void DeletedState_Calls_OnCancelled()
    {
        var filter = CreateFilter();
        var context = CreateContext(new DeletedState());

        filter.OnStateElection(context);

        _onCancelled.Received(1).OnCancelled(Arg.Is<ExecutionEvent>(e =>
            e.JobId == "test-job-1"));
    }

    [Fact]
    public void ScheduledState_FromFailed_Calls_OnRetrying()
    {
        var filter = CreateFilter();
        var scheduledState = new ScheduledState(TimeSpan.FromMinutes(1));
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();
        var bgJob = CreateTestJob();
        var backgroundJob = new BackgroundJob("test-job-1", bgJob, DateTime.UtcNow);

        connection.GetJobParameter(backgroundJob.Id, Infrastructure.HangfireDispatcher.EventId).Returns((string?)null);
        connection.GetJobParameter(backgroundJob.Id, "RetryCount").Returns("2");

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, scheduledState, "Failed");
        var context = new ElectStateContext(applyContext);

        filter.OnStateElection(context);

        _onRetrying.Received(1).OnRetrying(Arg.Is<ExecutionRetryingEvent>(e =>
            e.JobId == "test-job-1" && e.AttemptNumber == 2));
    }

    [Fact]
    public void EnqueuedState_FromFailed_Calls_OnRetrying_NotOnEnqueued()
    {
        var filter = CreateFilter();
        var enqueuedState = new EnqueuedState();
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();
        var bgJob = CreateTestJob();
        var backgroundJob = new BackgroundJob("test-job-1", bgJob, DateTime.UtcNow);

        connection.GetJobParameter(backgroundJob.Id, Infrastructure.HangfireDispatcher.EventId).Returns((string?)null);
        connection.GetJobParameter(backgroundJob.Id, "RetryCount").Returns("1");

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, enqueuedState, "Failed");
        var context = new ElectStateContext(applyContext);

        filter.OnStateElection(context);

        _onRetrying.Received(1).OnRetrying(Arg.Any<ExecutionRetryingEvent>());
        _onEnqueued.DidNotReceive().OnEnqueued(Arg.Any<ExecutionEvent>());
    }

    [Fact]
    public void CustomId_Passed_InEvent()
    {
        var filter = CreateFilter();
        var context = CreateContext(new EnqueuedState(), customId: "my-job");

        filter.OnStateElection(context);

        _onEnqueued.Received(1).OnEnqueued(Arg.Is<ExecutionEvent>(e =>
            e.CustomId == "my-job"));
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

    public class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
