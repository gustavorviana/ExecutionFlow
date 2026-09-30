using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;
using System.Reflection;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure.Filters;

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
        var bgJob = job ?? JobBuilder.CreateRecurringJob(typeof(TestHandler));
        var backgroundJob = new BackgroundJob("test-job-1", bgJob, DateTime.UtcNow);

        if (customId != null)
        {
            connection.GetJobParameter(backgroundJob.Id, ContextConsts.CustomId)
                .Returns(customId);
        }

        connection.GetJobParameter(backgroundJob.Id, "RetryCount")
            .Returns((string?)null);

        var applyContext = new ApplyStateContext(
            storage, connection, transaction, backgroundJob, candidateState, currentState);

        return new ElectStateContext(applyContext);
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

    // --- Real retry pipeline: Hangfire's AutomaticRetryAttribute (order 20) runs before our filter (order 100) ---

    private ElectStateContext ElectFailureThroughRetryFilter(AutomaticRetryAttribute retryFilter, Exception exception)
    {
        var context = CreateContext(new FailedState(exception), currentState: ProcessingState.StateName);
        retryFilter.OnStateElection(context);
        CreateFilter().OnStateElection(context);
        return context;
    }

    [Fact]
    public void FailedAttempt_ThatWillBeRetried_Calls_OnRetrying_WithException_NotOnFailed()
    {
        var exception = new InvalidOperationException("boom");

        ElectFailureThroughRetryFilter(new AutomaticRetryAttribute { Attempts = 3 }, exception);

        _onRetrying.Received(1).OnRetrying(Arg.Is<ExecutionRetryingEvent>(e => e.Exception == exception));
        _onFailed.DidNotReceiveWithAnyArgs().OnFailed(default!);
    }

    [Fact]
    public void FailedAttempt_WithNoRetriesLeft_Calls_OnFailed_NotOnRetrying()
    {
        var exception = new InvalidOperationException("boom");

        ElectFailureThroughRetryFilter(new AutomaticRetryAttribute { Attempts = 0 }, exception);

        _onFailed.Received(1).OnFailed(Arg.Is<ExecutionFailedEvent>(e => e.Exception == exception));
        _onRetrying.DidNotReceiveWithAnyArgs().OnRetrying(default!);
    }

    [Fact]
    public void RetriesExhausted_WithDeletePolicy_Calls_OnFailed_NotOnCancelled()
    {
        var exception = new InvalidOperationException("boom");

        ElectFailureThroughRetryFilter(
            new AutomaticRetryAttribute { Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete }, exception);

        _onFailed.Received(1).OnFailed(Arg.Is<ExecutionFailedEvent>(e => e.Exception == exception));
        _onCancelled.DidNotReceiveWithAnyArgs().OnCancelled(default!);
    }

    [Fact]
    public void ScheduledRetryBecomingDue_CallsNoHook()
    {
        var context = CreateContext(new EnqueuedState(), currentState: ScheduledState.StateName);
        context.Connection.GetJobParameter("test-job-1", "RetryCount").Returns("1");

        CreateFilter().OnStateElection(context);

        _onRetrying.DidNotReceiveWithAnyArgs().OnRetrying(default!);
        _onEnqueued.DidNotReceiveWithAnyArgs().OnEnqueued(default!);
    }

    // --- Duration (lifecycle-hooks AC-003.2) ---

    [Fact]
    public void Duration_IsMeasuredInUtc_OnAnyHostTimeZone()
    {
        var context = CreateContext(new SucceededState(null, 0, 0), currentState: ProcessingState.StateName);
        context.Connection.GetStateData("test-job-1").Returns(new StateData
        {
            Name = ProcessingState.StateName,
            Data = new Dictionary<string, string> { ["StartedAt"] = JobHelper.SerializeDateTime(DateTime.UtcNow.AddSeconds(-5)) }
        });

        CreateFilter().OnStateElection(context);

        _onSucceeded.Received(1).OnSucceeded(Arg.Is<ExecutionSucceededEvent>(e =>
            e.Duration >= TimeSpan.FromSeconds(4) && e.Duration < TimeSpan.FromMinutes(1)));
    }

    // --- Hook isolation (lifecycle-hooks REQ-004) ---

    private (HangfireStateFilter filter, IOnFailed second) CreateFilterWithThrowingHook(Action<HookErrorContext>? errorHandler)
    {
        var second = Substitute.For<IOnFailed>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ThrowingFailedHook)).Returns(new ThrowingFailedHook());
        serviceProvider.GetService(typeof(IOnFailed)).Returns(second);

        var filter = new HangfireStateFilter(Substitute.For<IExecutionFlowRegistry>(), serviceProvider,
            new List<Type> { typeof(ThrowingFailedHook), typeof(IOnFailed) }, errorHandler);
        return (filter, second);
    }

    [Fact]
    public void ThrowingHook_DoesNotStopOtherHooks_NorTheElection()
    {
        var (filter, second) = CreateFilterWithThrowingHook(null);
        var context = CreateContext(new FailedState(new Exception("job failed")));

        filter.OnStateElection(context);

        second.Received(1).OnFailed(Arg.Any<ExecutionFailedEvent>());
        Assert.IsType<FailedState>(context.CandidateState);
    }

    [Fact]
    public void ThrowingHook_IsReportedTo_HookErrorHandler()
    {
        HookErrorContext? reported = null;
        var (filter, _) = CreateFilterWithThrowingHook(e => reported = e);

        filter.OnStateElection(CreateContext(new FailedState(new Exception("job failed"))));

        Assert.NotNull(reported);
        Assert.Equal(typeof(ThrowingFailedHook), reported!.HookType);
        Assert.Equal(typeof(IOnFailed), reported.HookInterface);
        Assert.Equal("hook bug", reported.Exception.Message);
        Assert.Equal("test-job-1", reported.Event.JobId);
    }

    [Fact]
    public void ThrowingHookErrorHandler_IsSwallowed()
    {
        var (filter, second) = CreateFilterWithThrowingHook(_ => throw new Exception("handler bug"));

        filter.OnStateElection(CreateContext(new FailedState(new Exception("job failed"))));

        second.Received(1).OnFailed(Arg.Any<ExecutionFailedEvent>());
    }

    public class ThrowingFailedHook : IOnFailed
    {
        public void OnFailed(ExecutionFailedEvent e) => throw new InvalidOperationException("hook bug");
    }

    [Fact]
    public void EnqueuedState_FromFailed_Calls_OnRetrying_NotOnEnqueued()
    {
        var filter = CreateFilter();
        var enqueuedState = new EnqueuedState();
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var storage = Substitute.For<JobStorage>();
        var bgJob = JobBuilder.CreateRecurringJob(typeof(TestHandler));
        var backgroundJob = new BackgroundJob("test-job-1", bgJob, DateTime.UtcNow);

        connection.GetJobParameter(backgroundJob.Id, ContextConsts.CustomId).Returns((string?)null);
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

    [ExecutionFlow.Attributes.Recurring("* * * * *")]
    public class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
