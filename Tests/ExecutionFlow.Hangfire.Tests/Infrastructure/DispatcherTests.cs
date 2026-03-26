using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
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

    [Fact]
    public void Enqueue_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-99");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Publish(new TestEvent());

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    [Fact]
    public void Enqueue_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-100");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Publish(new TestNamedEvent());

        _connection.Received(1).SetJobParameter("job-100", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Enqueue_ReturnsJobId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-42");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        var jobId = dispatcher.Publish(new TestEvent());

        Assert.Equal("job-42", jobId);
    }

    // --- Schedule with TimeSpan ---

    [Fact]
    public void Schedule_TimeSpan_ReturnsJobId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-50");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        var jobId = dispatcher.Schedule(new TestEvent(), TimeSpan.FromMinutes(30));

        Assert.Equal("job-50", jobId);
    }

    [Fact]
    public void Schedule_TimeSpan_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-51");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Schedule(new TestNamedEvent(), TimeSpan.FromHours(1));

        _connection.Received(1).SetJobParameter("job-51", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Schedule_TimeSpan_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-52");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Schedule(new TestEvent(), TimeSpan.FromMinutes(5));

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    // --- Schedule with DateTimeOffset ---

    [Fact]
    public void Schedule_DateTimeOffset_ReturnsJobId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-60");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        var jobId = dispatcher.Schedule(new TestEvent(), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal("job-60", jobId);
    }

    [Fact]
    public void Schedule_DateTimeOffset_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-61");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Schedule(new TestNamedEvent(), DateTimeOffset.UtcNow.AddHours(2));

        _connection.Received(1).SetJobParameter("job-61", ContextConsts.CustomId, "named-job-1");
    }

    [Fact]
    public void Schedule_DateTimeOffset_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-62");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage, _jobIdGenerator, _registry);

        dispatcher.Schedule(new TestEvent(), DateTimeOffset.UtcNow.AddMinutes(10));

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    // Test types

    public class TestEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string CustomId => "named-job-1";
    }
}
