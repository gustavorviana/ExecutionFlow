using System;
using System.Collections.Generic;
using System.Linq;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class ExecutionManagerTests : IDisposable
{
    private readonly JobStorage _storage;
    private readonly IMonitoringApi _monitoringApi;
    private readonly IStorageConnection _connection;
    private readonly HangfireExecutionManager _manager;
    private readonly JobStorage? _previousStorage;

    public ExecutionManagerTests()
    {
        _previousStorage = TryGetCurrentStorage();
        _storage = Substitute.For<JobStorage>();
        _monitoringApi = Substitute.For<IMonitoringApi>();
        _connection = Substitute.For<IStorageConnection>();

        _storage.GetMonitoringApi().Returns(_monitoringApi);
        _storage.GetConnection().Returns(_connection);

        JobStorage.Current = _storage;

        _manager = new HangfireExecutionManager();
    }

    public void Dispose()
    {
        if (_previousStorage != null)
            JobStorage.Current = _previousStorage;
    }

    private static JobStorage? TryGetCurrentStorage()
    {
        try { return JobStorage.Current; } catch { return null; }
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

    private static global::Hangfire.Common.Job CreateGenericJob<TEvent>()
    {
        return global::Hangfire.Common.Job.FromExpression<HangfireJobDispatcher>(
            x => x.DispatchEventAsync<TEvent>(default, null, null, default));
    }

    [Fact]
    public void IsRunning_ReturnsTrue_WhenMatchingJobFound()
    {
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-1", new ProcessingJobDto())));
        _connection.GetJobParameter("job-1", "customId").Returns("my-job");

        var result = _manager.IsRunning("my-job");

        Assert.True(result);
    }

    [Fact]
    public void IsRunning_ReturnsFalse_WhenNoMatchingJob()
    {
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-1", new ProcessingJobDto())));
        _connection.GetJobParameter("job-1", "customId").Returns("other-job");

        var result = _manager.IsRunning("my-job");

        Assert.False(result);
    }

    [Fact]
    public void IsRunning_ReturnsFalse_WhenNoProcessingJobs()
    {
        _monitoringApi.ProcessingJobs(0, 100).Returns(ProcessingJobList());

        var result = _manager.IsRunning("my-job");

        Assert.False(result);
    }

    [Fact]
    public void IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound()
    {
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 100).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-2", new EnqueuedJobDto())));
        _connection.GetJobParameter("job-2", "customId").Returns("pending-job");

        var result = _manager.IsPending("pending-job");

        Assert.True(result);
    }

    [Fact]
    public void IsPending_ReturnsFalse_WhenNoMatchingJob()
    {
        var queues = new List<QueueWithTopEnqueuedJobsDto> { new() { Name = "default" } };
        _monitoringApi.Queues().Returns(queues);
        _monitoringApi.EnqueuedJobs("default", 0, 100).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-2", new EnqueuedJobDto())));
        _connection.GetJobParameter("job-2", "customId").Returns("other-job");

        var result = _manager.IsPending("pending-job");

        Assert.False(result);
    }

    [Fact]
    public void Cancel_QueriesProcessingJobs()
    {
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-3", new ProcessingJobDto())));
        _connection.GetJobParameter("job-3", "customId").Returns("cancel-me");
        _monitoringApi.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());

        _manager.Cancel("cancel-me");

        _monitoringApi.Received(1).ProcessingJobs(0, 100);
    }

    // --- GetJobs tests ---

    [Fact]
    public void GetJobs_Processing_ReturnsJobsWithCorrectJobInfo()
    {
        var startedAt = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateGenericJob<TestEvent>();
        var dto = new ProcessingJobDto { Job = job, StartedAt = startedAt };
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-10", dto)));
        _connection.GetJobParameter("job-10", "customId").Returns("my-custom-id");

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
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-11", dto)));
        _connection.GetJobParameter("job-11", "customId").Returns((string)null);

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
        _monitoringApi.ProcessingJobs(0, 100).Returns(
            ProcessingJobList(new KeyValuePair<string, ProcessingJobDto>("job-12", dto)));
        _connection.GetJobParameter("job-12", "customId").Returns((string)null);

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
        _monitoringApi.SucceededJobs(0, 100).Returns(
            SucceededJobList(new KeyValuePair<string, SucceededJobDto>("job-13", dto)));
        _connection.GetJobParameter("job-13", "customId").Returns((string)null);

        var results = _manager.GetJobs(JobState.Succeeded).ToList();

        Assert.Single(results);
        Assert.Equal(new DateTimeOffset(succeededAt), results[0].StateChangedAt);
    }

    [Fact]
    public void GetJobs_ReturnsEmpty_WhenNoJobsExist()
    {
        _monitoringApi.ProcessingJobs(0, 100).Returns(ProcessingJobList());

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
        _monitoringApi.EnqueuedJobs("default", 0, 100).Returns(
            EnqueuedJobList(new KeyValuePair<string, EnqueuedJobDto>("job-14", dto)));
        _connection.GetJobParameter("job-14", "customId").Returns("enqueued-id");

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
        _monitoringApi.FailedJobs(0, 100).Returns(
            FailedJobList(new KeyValuePair<string, FailedJobDto>("job-15", dto)));
        _connection.GetJobParameter("job-15", "customId").Returns((string)null);

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
        _monitoringApi.DeletedJobs(0, 100).Returns(
            DeletedJobList(new KeyValuePair<string, DeletedJobDto>("job-16", dto)));
        _connection.GetJobParameter("job-16", "customId").Returns((string)null);

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
        _monitoringApi.SucceededJobs(0, 100).Returns(
            SucceededJobList(new KeyValuePair<string, SucceededJobDto>("job-17", dto)));
        _connection.GetJobParameter("job-17", "customId").Returns("succeeded-id");

        var results = _manager.GetJobs(JobState.Succeeded).ToList();

        Assert.Single(results);
        Assert.Equal(JobState.Succeeded, results[0].State);
        Assert.Equal("succeeded-id", results[0].CustomId);
    }

    public class TestEvent { }
}
