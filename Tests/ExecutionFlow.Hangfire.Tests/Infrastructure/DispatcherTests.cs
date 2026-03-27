using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NSubstitute;
using HangfireDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireDispatcher;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class DispatcherTests
{
    private readonly JobStorage _storage;
    private readonly IStorageConnection _connection;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IJobIdGenerator _jobIdGenerator;
    private readonly IExecutionFlowRegistry _registry;

    public DispatcherTests()
    {
        _storage = Substitute.For<JobStorage>();
        _connection = Substitute.For<IStorageConnection>();
        _storage.GetConnection().Returns(_connection);
        _jobClient = Substitute.For<IBackgroundJobClient>();
        _jobIdGenerator = Substitute.For<IJobIdGenerator>();
        _registry = Substitute.For<IExecutionFlowRegistry>();
    }

    private HangfireDispatcher CreateDispatcher(HangfireOptions? options = null)
    {
        return new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry, options ?? new HangfireOptions());
    }

    // --- Publish ---

    [Fact]
    public void Publish_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-99");
        var dispatcher = CreateDispatcher();

        dispatcher.Publish(new TestEvent());

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    [Fact]
    public void Publish_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-100");
        var dispatcher = CreateDispatcher();

        dispatcher.Publish(new TestNamedEvent());

        _connection.Received(1).SetJobParameter("job-100", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Publish_ReturnsHangfireJobId_WhenNoCustomId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-42");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Publish(new TestEvent());

        Assert.Equal("job-42", result.JobId);
        Assert.True(result.Enqueued);
    }

    [Fact]
    public void Publish_ReturnsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-42");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.Equal("named-job-1", result.JobId);
        Assert.True(result.Enqueued);
    }

    // --- Schedule with TimeSpan ---

    [Fact]
    public void Schedule_TimeSpan_ReturnsHangfireJobId_WhenNoCustomId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-50");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Schedule(new TestEvent(), TimeSpan.FromMinutes(30));

        Assert.Equal("job-50", result.JobId);
        Assert.True(result.Enqueued);
    }

    [Fact]
    public void Schedule_TimeSpan_ReturnsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-50");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Schedule(new TestNamedEvent(), TimeSpan.FromMinutes(30));

        Assert.Equal("named-job-1", result.JobId);
        Assert.True(result.Enqueued);
    }

    [Fact]
    public void Schedule_TimeSpan_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-51");
        var dispatcher = CreateDispatcher();

        dispatcher.Schedule(new TestNamedEvent(), TimeSpan.FromHours(1));

        _connection.Received(1).SetJobParameter("job-51", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Schedule_TimeSpan_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-52");
        var dispatcher = CreateDispatcher();

        dispatcher.Schedule(new TestEvent(), TimeSpan.FromMinutes(5));

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    // --- Schedule with DateTimeOffset ---

    [Fact]
    public void Schedule_DateTimeOffset_ReturnsHangfireJobId_WhenNoCustomId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-60");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Schedule(new TestEvent(), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal("job-60", result.JobId);
        Assert.True(result.Enqueued);
    }

    [Fact]
    public void Schedule_DateTimeOffset_ReturnsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-60");
        var dispatcher = CreateDispatcher();

        var result = dispatcher.Schedule(new TestNamedEvent(), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal("named-job-1", result.JobId);
        Assert.True(result.Enqueued);
    }

    [Fact]
    public void Schedule_DateTimeOffset_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-61");
        var dispatcher = CreateDispatcher();

        dispatcher.Schedule(new TestNamedEvent(), DateTimeOffset.UtcNow.AddHours(2));

        _connection.Received(1).SetJobParameter("job-61", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Schedule_DateTimeOffset_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-62");
        var dispatcher = CreateDispatcher();

        dispatcher.Schedule(new TestEvent(), DateTimeOffset.UtcNow.AddMinutes(10));

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    // --- Deduplication: SkipIfExists ---

    [Fact]
    public void Publish_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning()
    {
        var monitoringApi = Substitute.For<IMonitoringApi>();
        _storage.GetMonitoringApi().Returns(monitoringApi);
        monitoringApi.ProcessingJobs(0, 10).Returns(new JobList<ProcessingJobDto>(new List<KeyValuePair<string, ProcessingJobDto>>
        {
            new KeyValuePair<string, ProcessingJobDto>("existing-job", new ProcessingJobDto { Job = null })
        }));
        _connection.GetJobParameter("existing-job", ContextConsts.CustomId).Returns("named-job-1");

        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.False(result.Enqueued);
        Assert.Null(result.JobId);
    }

    [Fact]
    public void Publish_SkipIfExists_EnqueuesNormally_WhenNoExistingJob()
    {
        var monitoringApi = Substitute.For<IMonitoringApi>();
        _storage.GetMonitoringApi().Returns(monitoringApi);
        monitoringApi.ProcessingJobs(0, 10).Returns(new JobList<ProcessingJobDto>(new List<KeyValuePair<string, ProcessingJobDto>>()));
        monitoringApi.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());

        _jobClient.Create(default, default).ReturnsForAnyArgs("job-new");
        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("named-job-1", result.JobId);
    }

    [Fact]
    public void Publish_SkipIfExists_IgnoresNonCustomIdEvents()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-70");
        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("job-70", result.JobId);
    }

    // --- Deduplication: ReplaceExisting ---

    [Fact]
    public void Publish_ReplaceExisting_CancelsAndEnqueuesNew()
    {
        var monitoringApi = Substitute.For<IMonitoringApi>();
        _storage.GetMonitoringApi().Returns(monitoringApi);
        monitoringApi.ProcessingJobs(0, 10).Returns(new JobList<ProcessingJobDto>(new List<KeyValuePair<string, ProcessingJobDto>>
        {
            new KeyValuePair<string, ProcessingJobDto>("old-job", new ProcessingJobDto { Job = null })
        }));
        _connection.GetJobParameter("old-job", ContextConsts.CustomId).Returns("named-job-1");
        _jobClient.Create(default, default).ReturnsForAnyArgs("new-job");

        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.ReplaceExisting };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("named-job-1", result.JobId);
    }

    // --- Deduplication: Disabled ---

    [Fact]
    public void Publish_Disabled_AlwaysEnqueues()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-80");
        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.Disabled };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("named-job-1", result.JobId);
    }

    // Test types

    public class TestEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string CustomId => "named-job-1";
    }
}
