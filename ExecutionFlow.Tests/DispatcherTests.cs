using System;
using System.Collections.Generic;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class DispatcherTests : IDisposable
{
    private readonly JobStorage _storage;
    private readonly IStorageConnection _connection;
    private readonly JobStorage? _previousStorage;

    public DispatcherTests()
    {
        _previousStorage = TryGetCurrentStorage();
        _storage = Substitute.For<JobStorage>();
        _connection = Substitute.For<IStorageConnection>();
        _storage.GetConnection().Returns(_connection);
        JobStorage.Current = _storage;
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

    [Fact]
    public void Enqueue_ThrowsWhenNoHandlerRegistered()
    {
        // Configure ExecutionFlowSetup with no registrations for TestEvent
        ExecutionFlowSetup.Configure(options =>
        {
            // Empty - no handlers registered
        });

        var dispatcher = new HangfireDispatcher();

        Assert.Throws<InvalidOperationException>(() => dispatcher.Enqueue(new UnregisteredEvent()));
    }

    [Fact]
    public void Enqueue_ThrowsWithDescriptiveMessage()
    {
        ExecutionFlowSetup.Configure(options => { });

        var dispatcher = new HangfireDispatcher();

        var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Enqueue(new UnregisteredEvent()));
        Assert.Contains(nameof(UnregisteredEvent), ex.Message);
    }

    public class UnregisteredEvent { }

    public class TestNamedEvent : ICustomIdEvent
    {
        public string GetCustomId() => "named-job-1";
    }

    public class TestNamedEventHandler : IHandler<TestNamedEvent>
    {
        public Task HandleAsync(ExecutionContext<TestNamedEvent> context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
