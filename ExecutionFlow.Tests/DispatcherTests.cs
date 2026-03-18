using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Tests;

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

    private HangfireSetup CreateSetup(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        return setup;
    }

    [Fact]
    public void Enqueue_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent()
    {
        var setup = CreateSetup(options => options.Add(typeof(TestEventHandler)));
        _jobClient.Create(default, default).ReturnsForAnyArgs("job-99");

        var dispatcher = new HangfireDispatcher(_jobClient, _storage);

        dispatcher.Enqueue(new TestEvent());

        _connection.DidNotReceiveWithAnyArgs().SetJobParameter(default, default, default);
    }

    public class UnregisteredEvent { }

    public class TestEvent { }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string GetCustomId() => "named-job-1";
    }

    public class TestNamedEventHandler : IHandler<TestNamedEvent>
    {
        public Task HandleAsync(FlowContext<TestNamedEvent> context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
