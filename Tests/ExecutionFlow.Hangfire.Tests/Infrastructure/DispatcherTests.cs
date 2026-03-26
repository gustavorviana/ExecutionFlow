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

    public class TestEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string CustomId => "named-job-1";
    }
}
