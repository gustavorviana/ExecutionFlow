using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using HangfireJobDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class ExecutionManagerTests
{
    private readonly JobStorage _storage;
    private readonly IMonitoringApi _monitoringApi;
    private readonly IStorageConnection _connection;
    private readonly IBackgroundJobClient _jobClient;
    private readonly HangfireExecutionManager _manager;

    public ExecutionManagerTests()
    {
        _storage = Substitute.For<JobStorage>();
        _monitoringApi = Substitute.For<IMonitoringApi>();
        _connection = Substitute.For<IStorageConnection>();
        _jobClient = Substitute.For<IBackgroundJobClient>();

        _storage.GetMonitoringApi().Returns(_monitoringApi);
        _storage.GetConnection().Returns(_connection);

        _manager = new HangfireExecutionManager(_jobClient, _storage);
    }

    private static JobList<ProcessingJobDto> ProcessingJobList(params KeyValuePair<string, ProcessingJobDto>[] items)
        => new JobList<ProcessingJobDto>(items);

    private static JobList<EnqueuedJobDto> EnqueuedJobList(params KeyValuePair<string, EnqueuedJobDto>[] items)
        => new JobList<EnqueuedJobDto>(items);

    private static JobList<SucceededJobDto> SucceededJobList(params KeyValuePair<string, SucceededJobDto>[] items)
        => new JobList<SucceededJobDto>(items);

    private static JobList<FailedJobDto> FailedJobList(params KeyValuePair<string, FailedJobDto>[] items)
        => new JobList<FailedJobDto>(items);

    private static JobList<DeletedJobDto> DeletedJobList(params KeyValuePair<string, DeletedJobDto>[] items)
        => new JobList<DeletedJobDto>(items);

    private static Job CreateGenericJob<TEvent>()
    {
        return Job.FromExpression<HangfireJobDispatcher>(
            x => x.DispatchEventAsync<TEvent>(default!, null, null, default));
    }

    [Fact]
    public void IsRunning_ReturnsTrue_WhenMatchingByHangfireJobId()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-1", new ProcessingJobDto())));

        var result = _manager.IsRunning("job-1");

        Assert.True(result);
    }

    [Fact]
    public void IsRunning_ReturnsTrue_WhenMatchingByCustomId()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-1", new ProcessingJobDto())));
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns("my-job");

        var result = _manager.IsRunning("my-job");

        Assert.True(result);
    }

    [Fact]
    public void IsRunning_ReturnsFalse_WhenNoMatchingJob()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-1", new ProcessingJobDto())));
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns("other-job");

        var result = _manager.IsRunning("my-job");

        Assert.False(result);
    }

    [Fact]
    public void IsRunning_ReturnsFalse_WhenNoProcessingJobs()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(ProcessingJobList());

        var result = _manager.IsRunning("my-job");

        Assert.False(result);
    }

    [Fact]
    public void IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound()
    {
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-2", new EnqueuedJobDto())));
        _connection.GetJobParameter("job-2", ContextConsts.CustomId).Returns("pending-job");

        var result = _manager.IsPending("pending-job");

        Assert.True(result);
    }

    [Fact]
    public void IsPending_ReturnsFalse_WhenNoMatchingJob()
    {
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-2", new EnqueuedJobDto())));
        _connection.GetJobParameter("job-2", ContextConsts.CustomId).Returns("other-job");

        var result = _manager.IsPending("pending-job");

        Assert.False(result);
    }

    [Fact]
    public void Cancel_QueriesProcessingJobs()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-3", new ProcessingJobDto())));
        _connection.GetJobParameter("job-3", ContextConsts.CustomId).Returns("cancel-me");
        _monitoringApi.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());

        _manager.Cancel("cancel-me");

        _monitoringApi.Received(1).ProcessingJobs(0, 10);
    }

    // --- GetJobs tests ---

    [Fact]
    public void GetJobs_Processing_ReturnsJobsWithCorrectJobInfo()
    {
        var startedAt = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateGenericJob<TestEvent>();
        var dto = new ProcessingJobDto { Job = job, StartedAt = startedAt };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-10", dto)));
        _connection.GetJobParameter("job-10", ContextConsts.CustomId).Returns("my-custom-id");

        var results = _manager.GetJobs(JobState.Processing).ToList();

        Assert.Single(results);
        Assert.Equal("job-10", results[0].JobId);
        Assert.Equal("my-custom-id", results[0].CustomId);
        Assert.Equal(nameof(TestEvent), results[0].EventTypeName);
        Assert.Equal(typeof(TestEvent), results[0].EventType);
        Assert.Equal(JobState.Processing, results[0].State);
        Assert.Equal(new DateTimeOffset(startedAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_CustomIdIsNull_WhenNoCustomIdParameter()
    {
        var dto = new ProcessingJobDto { Job = CreateGenericJob<TestEvent>() };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-11", dto)));
        _connection.GetJobParameter("job-11", ContextConsts.CustomId).Returns((string)null);

        var results = _manager.GetJobs(JobState.Processing).ToList();

        Assert.Single(results);
        Assert.Null(results[0].CustomId);
    }

    [Fact]
    public void GetJobs_EventTypeIsNull_WhenJobIsNull()
    {
        var dto = new ProcessingJobDto
        {
            Job = null,
            InvocationData = new InvocationData("SomeType", "SomeMethod", "[]", "[]")
        };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-12", dto)));
        _connection.GetJobParameter("job-12", ContextConsts.CustomId).Returns((string)null);

        var results = _manager.GetJobs(JobState.Processing).ToList();

        Assert.Single(results);
        Assert.Null(results[0].EventType);
        Assert.Equal("SomeMethod", results[0].EventTypeName);
    }

    [Fact]
    public void GetJobs_StateChangedAt_PopulatedCorrectly()
    {
        var succeededAt = new DateTime(2026, 3, 7, 14, 30, 0, DateTimeKind.Utc);
        var dto = new SucceededJobDto { Job = CreateGenericJob<TestEvent>(), SucceededAt = succeededAt };
        _monitoringApi.SucceededJobs(0, 10).Returns(
            SucceededJobList(new KeyValuePair<string, SucceededJobDto>("job-13", dto)));
        _connection.GetJobParameter("job-13", ContextConsts.CustomId).Returns((string)null);

        var results = _manager.GetJobs(JobState.Succeeded).ToList();

        Assert.Single(results);
        Assert.Equal(new DateTimeOffset(succeededAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_ReturnsEmpty_WhenNoJobsExist()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(ProcessingJobList());

        var results = _manager.GetJobs(JobState.Processing).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void GetJobs_Enqueued_QueriesEnqueuedJobs()
    {
        var enqueuedAt = new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc);
        var dto = new EnqueuedJobDto { Job = CreateGenericJob<TestEvent>(), EnqueuedAt = enqueuedAt };
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-14", dto)));
        _connection.GetJobParameter("job-14", ContextConsts.CustomId).Returns("enqueued-id");

        var results = _manager.GetJobs(JobState.Enqueued).ToList();

        Assert.Single(results);
        Assert.Equal(JobState.Enqueued, results[0].State);
        Assert.Equal("enqueued-id", results[0].CustomId);
        Assert.Equal(new DateTimeOffset(enqueuedAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_Failed_QueriesFailedJobs()
    {
        var failedAt = new DateTime(2026, 3, 7, 11, 0, 0, DateTimeKind.Utc);
        var dto = new FailedJobDto { Job = CreateGenericJob<TestEvent>(), FailedAt = failedAt };
        _monitoringApi.FailedJobs(0, 10).Returns(
            FailedJobList(new KeyValuePair<string, FailedJobDto>("job-15", dto)));
        _connection.GetJobParameter("job-15", ContextConsts.CustomId).Returns((string)null);

        var results = _manager.GetJobs(JobState.Failed).ToList();

        Assert.Single(results);
        Assert.Equal(JobState.Failed, results[0].State);
        Assert.Equal(new DateTimeOffset(failedAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_Cancelled_QueriesDeletedJobs()
    {
        var deletedAt = new DateTime(2026, 3, 7, 13, 0, 0, DateTimeKind.Utc);
        var dto = new DeletedJobDto { Job = CreateGenericJob<TestEvent>(), DeletedAt = deletedAt };
        _monitoringApi.DeletedJobs(0, 10).Returns(
            DeletedJobList(new KeyValuePair<string, DeletedJobDto>("job-16", dto)));
        _connection.GetJobParameter("job-16", ContextConsts.CustomId).Returns((string)null);

        var results = _manager.GetJobs(JobState.Cancelled).ToList();

        Assert.Single(results);
        Assert.Equal(JobState.Cancelled, results[0].State);
        Assert.Equal(new DateTimeOffset(deletedAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_Succeeded_QueriesSucceededJobs()
    {
        var succeededAt = new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc);
        var dto = new SucceededJobDto { Job = CreateGenericJob<TestEvent>(), SucceededAt = succeededAt };
        _monitoringApi.SucceededJobs(0, 10).Returns(
            SucceededJobList(new KeyValuePair<string, SucceededJobDto>("job-17", dto)));
        _connection.GetJobParameter("job-17", ContextConsts.CustomId).Returns("succeeded-id");

        var results = _manager.GetJobs(JobState.Succeeded).ToList();

        Assert.Single(results);
        Assert.Equal(JobState.Succeeded, results[0].State);
        Assert.Equal("succeeded-id", results[0].CustomId);
    }

    // --- Retry ---

    [Fact]
    public void Retry_RequeuesFailedJob_ReturnsTrue()
    {
        _monitoringApi.FailedJobs(0, 10).Returns(FailedJobList(
            new KeyValuePair<string, FailedJobDto>("failed-job-1", new FailedJobDto { Job = null })));

        _connection.GetJobParameter("failed-job-1", ContextConsts.CustomId).Returns("my-custom-id");
        _jobClient.ChangeState(Arg.Any<string>(), Arg.Any<global::Hangfire.States.IState>(), Arg.Any<string>()).Returns(true);

        var result = _manager.Retry("my-custom-id");

        Assert.True(result);
    }

    [Fact]
    public void Retry_ReturnsFalse_WhenNoFailedJobWithCustomId()
    {
        _monitoringApi.FailedJobs(0, 10).Returns(FailedJobList());

        var result = _manager.Retry("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public void Retry_ReturnsFalse_WhenCustomIdDoesNotMatch()
    {
        _monitoringApi.FailedJobs(0, 10).Returns(FailedJobList(
            new KeyValuePair<string, FailedJobDto>("failed-job-2", new FailedJobDto { Job = null })));

        _connection.GetJobParameter("failed-job-2", ContextConsts.CustomId).Returns("other-id");

        var result = _manager.Retry("my-custom-id");

        Assert.False(result);
    }

    // --- Recurring by Type: IsRunning ---

    private static Job CreateRecurringJob(Type handlerType)
    {
        return Job.FromExpression<HangfireJobDispatcher>(
            x => x.DispatchRecurringAsync(null, handlerType, default));
    }

    [Fact]
    public void IsRunning_Type_ReturnsTrue_WhenMatchingRecurringJobProcessing()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new ProcessingJobDto { Job = job };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("rjob-1", dto)));

        var result = _manager.IsRunning(typeof(TestRecurringHandler));

        Assert.True(result);
    }

    [Fact]
    public void IsRunning_Type_ReturnsFalse_WhenDifferentHandlerType()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new ProcessingJobDto { Job = job };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("rjob-2", dto)));

        var result = _manager.IsRunning(typeof(OtherRecurringHandler));

        Assert.False(result);
    }

    [Fact]
    public void IsRunning_Type_ReturnsFalse_WhenNoProcessingJobs()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(ProcessingJobList());

        var result = _manager.IsRunning(typeof(TestRecurringHandler));

        Assert.False(result);
    }

    [Fact]
    public void IsRunning_Type_ReturnsFalse_WhenJobIsEventNotRecurring()
    {
        var job = CreateGenericJob<TestEvent>();
        var dto = new ProcessingJobDto { Job = job };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("rjob-3", dto)));

        var result = _manager.IsRunning(typeof(TestRecurringHandler));

        Assert.False(result);
    }

    // --- Recurring by Type: IsPending ---

    [Fact]
    public void IsPending_Type_ReturnsTrue_WhenMatchingRecurringJobEnqueued()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new EnqueuedJobDto { Job = job };
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("rjob-4", dto)));

        var result = _manager.IsPending(typeof(TestRecurringHandler));

        Assert.True(result);
    }

    [Fact]
    public void IsPending_Type_ReturnsFalse_WhenDifferentHandlerType()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new EnqueuedJobDto { Job = job };
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("rjob-5", dto)));

        var result = _manager.IsPending(typeof(OtherRecurringHandler));

        Assert.False(result);
    }

    [Fact]
    public void IsPending_Type_ReturnsFalse_WhenNoEnqueuedJobs()
    {
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(EnqueuedJobList());

        var result = _manager.IsPending(typeof(TestRecurringHandler));

        Assert.False(result);
    }

    // --- Recurring by Type: Cancel ---

    [Fact]
    public void Cancel_Type_DeletesProcessingRecurringJob()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new ProcessingJobDto { Job = job };
        _monitoringApi.ProcessingJobs(0, 10).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("rjob-6", dto)));

        _manager.Cancel(typeof(TestRecurringHandler));

        _jobClient.Received(1).ChangeState("rjob-6", Arg.Any<global::Hangfire.States.IState>(), Arg.Any<string>());
    }

    [Fact]
    public void Cancel_Type_DeletesEnqueuedRecurringJob_WhenNotProcessing()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(ProcessingJobList());

        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new EnqueuedJobDto { Job = job };
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("rjob-7", dto)));

        _manager.Cancel(typeof(TestRecurringHandler));

        _jobClient.Received(1).ChangeState("rjob-7", Arg.Any<global::Hangfire.States.IState>(), Arg.Any<string>());
    }

    [Fact]
    public void Cancel_Type_DoesNothing_WhenNoMatchingJob()
    {
        _monitoringApi.ProcessingJobs(0, 10).Returns(ProcessingJobList());
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 10).Returns(EnqueuedJobList());

        _manager.Cancel(typeof(TestRecurringHandler));

        _jobClient.DidNotReceive().ChangeState(Arg.Any<string>(), Arg.Any<global::Hangfire.States.IState>(), Arg.Any<string>());
    }

    // --- Recurring by Type: Retry ---

    [Fact]
    public void Retry_Type_RequeuesFailedRecurringJob_ReturnsTrue()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new FailedJobDto { Job = job };
        _monitoringApi.FailedJobs(0, 10).Returns(
            FailedJobList(new KeyValuePair<string, FailedJobDto>("rjob-8", dto)));
        _jobClient.ChangeState(Arg.Any<string>(), Arg.Any<global::Hangfire.States.IState>(), Arg.Any<string>()).Returns(true);

        var result = _manager.Retry(typeof(TestRecurringHandler));

        Assert.True(result);
    }

    [Fact]
    public void Retry_Type_ReturnsFalse_WhenNoFailedRecurringJob()
    {
        _monitoringApi.FailedJobs(0, 10).Returns(FailedJobList());

        var result = _manager.Retry(typeof(TestRecurringHandler));

        Assert.False(result);
    }

    [Fact]
    public void Retry_Type_ReturnsFalse_WhenDifferentHandlerType()
    {
        var job = CreateRecurringJob(typeof(TestRecurringHandler));
        var dto = new FailedJobDto { Job = job };
        _monitoringApi.FailedJobs(0, 10).Returns(
            FailedJobList(new KeyValuePair<string, FailedJobDto>("rjob-9", dto)));

        var result = _manager.Retry(typeof(OtherRecurringHandler));

        Assert.False(result);
    }

    [Fact]
    public void Retry_Type_ReturnsFalse_WhenFailedJobIsEventNotRecurring()
    {
        var job = CreateGenericJob<TestEvent>();
        var dto = new FailedJobDto { Job = job };
        _monitoringApi.FailedJobs(0, 10).Returns(
            FailedJobList(new KeyValuePair<string, FailedJobDto>("rjob-10", dto)));

        var result = _manager.Retry(typeof(TestRecurringHandler));

        Assert.False(result);
    }

    // --- CountJobs ---

    private StatisticsDto CreateStats(long enqueued = 0, long processing = 0, long succeeded = 0, long failed = 0, long deleted = 0)
    {
        return new StatisticsDto { Enqueued = enqueued, Processing = processing, Succeeded = succeeded, Failed = failed, Deleted = deleted };
    }

    [Theory]
    [InlineData(JobState.Enqueued, 5)]
    [InlineData(JobState.Processing, 3)]
    [InlineData(JobState.Succeeded, 10)]
    [InlineData(JobState.Failed, 2)]
    [InlineData(JobState.Cancelled, 7)]
    public void CountJobs_ReturnsCorrectCount(JobState state, long expected)
    {
        _monitoringApi.GetStatistics().Returns(CreateStats(enqueued: 5, processing: 3, succeeded: 10, failed: 2, deleted: 7));

        var result = _manager.CountJobs(state);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CountJobs_ReturnsZero_WhenNoJobs()
    {
        _monitoringApi.GetStatistics().Returns(CreateStats());

        Assert.Equal(0, _manager.CountJobs(JobState.Enqueued));
        Assert.Equal(0, _manager.CountJobs(JobState.Processing));
        Assert.Equal(0, _manager.CountJobs(JobState.Succeeded));
        Assert.Equal(0, _manager.CountJobs(JobState.Failed));
        Assert.Equal(0, _manager.CountJobs(JobState.Cancelled));
    }

    // --- GetStateSummary ---

    [Fact]
    public void GetStateSummary_ReturnsAllCounts()
    {
        _monitoringApi.GetStatistics().Returns(CreateStats(enqueued: 1, processing: 2, succeeded: 3, failed: 4, deleted: 5));

        var summary = _manager.GetStateSummary();

        Assert.Equal(1, summary.Enqueued);
        Assert.Equal(2, summary.Processing);
        Assert.Equal(3, summary.Succeeded);
        Assert.Equal(4, summary.Failed);
        Assert.Equal(5, summary.Cancelled);
    }

    [Fact]
    public void GetStateSummary_ReturnsZeros_WhenNoJobs()
    {
        _monitoringApi.GetStatistics().Returns(CreateStats());

        var summary = _manager.GetStateSummary();

        Assert.Equal(0, summary.Enqueued);
        Assert.Equal(0, summary.Processing);
        Assert.Equal(0, summary.Succeeded);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Cancelled);
    }

    public class TestEvent { }
    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public class OtherRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
