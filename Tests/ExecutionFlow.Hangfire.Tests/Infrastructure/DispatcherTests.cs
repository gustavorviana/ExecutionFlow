using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using Hangfire.States;
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

        _connection.Received(1).SetJobParameter("job-100", ContextConsts.CustomId, JobParameters.EncodeCustomId("named-job-1"));
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

        _connection.Received(1).SetJobParameter("job-51", ContextConsts.CustomId, JobParameters.EncodeCustomId("named-job-1"));
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

        _connection.Received(1).SetJobParameter("job-61", ContextConsts.CustomId, JobParameters.EncodeCustomId("named-job-1"));
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
        SetupRunningJob("existing-job", "named-job-1");

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
        SetupRunningJob("old-job", "named-job-1");
        _jobClient.Create(default, default).ReturnsForAnyArgs("new-job");

        var options = new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.ReplaceExisting };
        var dispatcher = CreateDispatcher(options);

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("named-job-1", result.JobId);
        _jobClient.Received(1).ChangeState("old-job", Arg.Any<global::Hangfire.States.DeletedState>(), Arg.Any<string>());
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

    // --- Null event (AC-001.3, AC-002.2, AC-003.2) ---

    [Fact]
    public void Publish_ThrowsArgumentNullException_WhenEventIsNull()
    {
        var dispatcher = CreateDispatcher();

        Assert.Throws<ArgumentNullException>(() => dispatcher.Publish<TestEvent>(null!));
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    [Fact]
    public void Schedule_TimeSpan_ThrowsArgumentNullException_WhenEventIsNull()
    {
        var dispatcher = CreateDispatcher();

        Assert.Throws<ArgumentNullException>(() => dispatcher.Schedule<TestEvent>(null!, TimeSpan.FromMinutes(1)));
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    [Fact]
    public void Schedule_DateTimeOffset_ThrowsArgumentNullException_WhenEventIsNull()
    {
        var dispatcher = CreateDispatcher();

        Assert.Throws<ArgumentNullException>(() => dispatcher.Schedule<TestEvent>(null!, DateTimeOffset.UtcNow.AddMinutes(1)));
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    // --- Null or empty CustomId (AC-004.3, F-009) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Publish_TreatsEmptyCustomIdAsNone(string? customId)
    {
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-5");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Publish(new TestCustomIdEvent(customId));

        Assert.True(result.Enqueued);
        Assert.Equal("job-5", result.JobId);
        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default!, default!, default!);
        _storage.DidNotReceive().GetMonitoringApi();
    }

    [Fact]
    public void Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty()
    {
        SetupRunningJob("unrelated-job", customId: null);
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-6");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.ReplaceExisting });

        var result = dispatcher.Publish(new TestCustomIdEvent(""));

        Assert.True(result.Enqueued);
        _jobClient.DidNotReceiveWithAnyArgs().ChangeState(default!, default!, default!);
    }

    // --- Deduplication on Schedule (AC-004.4, F-004) ---

    [Fact]
    public void Schedule_TimeSpan_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning()
    {
        SetupRunningJob("existing-job", "named-job-1");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Schedule(new TestNamedEvent(), TimeSpan.FromMinutes(1));

        Assert.False(result.Enqueued);
        Assert.Null(result.JobId);
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    [Fact]
    public void Schedule_DateTimeOffset_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning()
    {
        SetupRunningJob("existing-job", "named-job-1");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Schedule(new TestNamedEvent(), DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.False(result.Enqueued);
        Assert.Null(result.JobId);
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    // --- Custom name (AC-005.1, F-005) ---

    [Fact]
    public void Publish_StoresCustomNameInJobArgs_WhenEventImplementsICustomNameEvent()
    {
        global::Hangfire.Common.Job? captured = null;
        _jobClient.Create(Arg.Do<global::Hangfire.Common.Job>(j => captured = j), Arg.Any<global::Hangfire.States.IState>()).Returns("job-7");
        var dispatcher = CreateDispatcher();

        dispatcher.Publish(new TestCustomNameEvent());

        Assert.NotNull(captured);
        Assert.Equal("Custom job name", captured!.Args[HangfireEventJobInfo.CustomNameIndex]);
    }

    // --- Atomic custom ID (AC-007.1, F-002) ---

    [Fact]
    public void Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2()
    {
        var client = Substitute.For<IBackgroundJobClientV2>();
        IDictionary<string, object>? parameters = null;
        client.Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<global::Hangfire.States.IState>(), Arg.Do<IDictionary<string, object>>(p => parameters = p))
            .Returns("job-v2");
        var dispatcher = new HangfireDispatcher(client, _storage, _jobIdGenerator, _registry, new HangfireOptions());

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.Equal("named-job-1", result.JobId);
        Assert.NotNull(parameters);
        Assert.Equal("named-job-1", parameters![ContextConsts.CustomId]);
        client.DidNotReceiveWithAnyArgs().Create(default!, default!);
        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default!, default!, default!);
    }

    private void SetupRunningJob(string jobId, string? customId)
    {
        var monitoringApi = Substitute.For<IMonitoringApi>();
        _storage.GetMonitoringApi().Returns(monitoringApi);
        monitoringApi.ProcessingJobs(0, 10).Returns(new JobList<ProcessingJobDto>(new List<KeyValuePair<string, ProcessingJobDto>>
        {
            new KeyValuePair<string, ProcessingJobDto>(jobId, new ProcessingJobDto { Job = null })
        }));
        monitoringApi.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());
        _connection.GetJobParameter(jobId, ContextConsts.CustomId).Returns(customId);

        if (customId != null)
            SetupReservation(customId, jobId, "Processing");
    }

    private void SetupReservation(string customId, string jobId, string stateName)
    {
        _connection.GetAllEntriesFromHash(DeduplicationStore.GetKey(customId))
            .Returns(new Dictionary<string, string> { [DeduplicationStore.JobIdField] = jobId });
        _connection.GetStateData(jobId).Returns(new StateData { Name = stateName });
    }

    // --- Reservation key (custom-id-and-deduplication REQ-007, REQ-009) ---

    [Fact]
    public void Publish_SkipIfExists_ReturnsFalse_WhenReservedJobIsScheduled()
    {
        SetupReservation("named-job-1", "scheduled-job", "Scheduled");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.False(result.Enqueued);
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    [Theory]
    [InlineData("Succeeded")]
    [InlineData("Deleted")]
    [InlineData("Failed")]
    public void Publish_SkipIfExists_Creates_WhenReservedJobIsNoLongerActive(string stateName)
    {
        SetupReservation("named-job-1", "old-job", stateName);
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("new-job");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        Assert.Equal("named-job-1", result.JobId);
    }

    [Fact]
    public void Publish_ReplaceExisting_DeletesReservedJob()
    {
        SetupReservation("named-job-1", "old-job", "Enqueued");
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("new-job");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.ReplaceExisting });

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        _jobClient.Received(1).ChangeState("old-job", Arg.Any<global::Hangfire.States.DeletedState>(), Arg.Any<string>());
    }

    [Fact]
    public void Publish_WritesReservationKey_WhenDeduplicationEnabled()
    {
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        _connection.CreateWriteTransaction().Returns(transaction);
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-42");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        dispatcher.Publish(new TestNamedEvent());

        transaction.Received(1).SetRangeInHash(DeduplicationStore.GetKey("named-job-1"),
            Arg.Is<IEnumerable<KeyValuePair<string, string>>>(e => e.Any(kv => kv.Key == DeduplicationStore.JobIdField && kv.Value == "job-42")));
        transaction.Received(1).Commit();
    }

    [Fact]
    public void Publish_DoesNotLockOrReserve_WhenDeduplicationDisabled()
    {
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-43");
        var dispatcher = CreateDispatcher();

        dispatcher.Publish(new TestNamedEvent());

        _connection.DidNotReceiveWithAnyArgs().AcquireDistributedLock(default!, default);
        _connection.DidNotReceiveWithAnyArgs().CreateWriteTransaction();
    }

    [Fact]
    public void Publish_AcquiresLockPerCustomId_WithConfiguredTimeout()
    {
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-44");
        var timeout = TimeSpan.FromMilliseconds(250);
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists, DeduplicationLockTimeout = timeout });

        dispatcher.Publish(new TestNamedEvent());

        _connection.Received(1).AcquireDistributedLock(DeduplicationStore.GetLockResource("named-job-1"), timeout);
    }

    [Fact]
    public void Publish_Throws_WhenLockTimesOut()
    {
        _connection.AcquireDistributedLock(default!, default).ReturnsForAnyArgs(_ => throw new DistributedLockTimeoutException("lock"));
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        Assert.Throws<DistributedLockTimeoutException>(() => dispatcher.Publish(new TestNamedEvent()));
        _jobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }

    [Fact]
    public void Publish_CreatesWithoutReservation_WhenLockTimesOutAndOptedIn()
    {
        _connection.AcquireDistributedLock(default!, default).ReturnsForAnyArgs(_ => throw new DistributedLockTimeoutException("lock"));
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-45");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists, CreateOnDeduplicationLockTimeout = true });

        var result = dispatcher.Publish(new TestNamedEvent());

        Assert.True(result.Enqueued);
        _connection.DidNotReceiveWithAnyArgs().CreateWriteTransaction();
    }

    // --- Per-event behavior (custom-id-and-deduplication REQ-006) ---

    [Fact]
    public void Publish_UsesEventAttribute_OverGlobalBehavior()
    {
        SetupReservation("attr-1", "existing-job", "Processing");
        var dispatcher = CreateDispatcher(); // global: Disabled

        var result = dispatcher.Publish(new SkipIfExistsEvent());

        Assert.False(result.Enqueued);
    }

    [Fact]
    public void Publish_EventAttributeDisabled_OverridesGlobalSkipIfExists()
    {
        SetupReservation("attr-2", "existing-job", "Processing");
        _jobClient.Create(default!, default!).ReturnsForAnyArgs("job-46");
        var dispatcher = CreateDispatcher(new HangfireOptions { DeduplicationBehavior = DeduplicationBehavior.SkipIfExists });

        var result = dispatcher.Publish(new NoDeduplicationEvent());

        Assert.True(result.Enqueued);
        _connection.DidNotReceiveWithAnyArgs().AcquireDistributedLock(default!, default);
    }

    [ExecutionFlow.Attributes.Deduplication(DeduplicationBehavior.SkipIfExists)]
    public class SkipIfExistsEvent : ICustomIdEvent
    {
        public string CustomId => "attr-1";
    }

    [ExecutionFlow.Attributes.Deduplication(DeduplicationBehavior.Disabled)]
    public class NoDeduplicationEvent : ICustomIdEvent
    {
        public string CustomId => "attr-2";
    }

    // Test types

    public class TestCustomIdEvent(string? customId) : ICustomIdEvent
    {
        public string CustomId => customId!;
    }

    public class TestCustomNameEvent : ICustomNameEvent
    {
        public string CustomName => "Custom job name";
    }

    public class TestEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string CustomId => "named-job-1";
    }
}
