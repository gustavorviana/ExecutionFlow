using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using HangfireDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireDispatcher;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

public class DispatcherTests
{
    private readonly JobStorage _storage;
    private readonly IStorageConnection _connection;
    private readonly IBackgroundJobClient _jobClient;

    public DispatcherTests()
    {
        _storage = Substitute.For<JobStorage>();
        _connection = Substitute.For<IStorageConnection>();
        _storage.GetConnection().Returns(_connection);
        _jobClient = Substitute.For<IBackgroundJobClient>();
    }

    [Fact]
    public void Enqueue_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-99");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage);

        dispatcher.Publish(new TestEvent());

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    [Fact]
    public void Enqueue_SetsCustomId_WhenEventImplementsICustomIdEvent()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-100");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage);

        dispatcher.Publish(new TestNamedEvent());

        _connection.Received(1).SetJobParameter("job-100", HangfireDispatcher.EventId, "named-job-1");
    }

    [Fact]
    public void Enqueue_ReturnsJobId()
    {
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-42");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage);

        var jobId = dispatcher.Publish(new TestEvent());

        Assert.Equal("job-42", jobId);
    }

    public class TestEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string CustomId => "named-job-1";
    }
}
