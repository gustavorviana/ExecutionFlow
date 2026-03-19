using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire;
using Hangfire;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class ExecutionFlowOptionsTests
{
    [Fact]
    public void Scan_Populates_Registrations()
    {
        var options = new HangfireOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly);

        Assert.NotEmpty(options.HandlerTypes);
    }

    [Fact]
    public void Add_Populates_Registrations()
    {
        var options = new HangfireOptions();

        options.Add(typeof(TestHandler));

        Assert.Single(options.HandlerTypes);
        Assert.Equal(typeof(TestHandler), options.HandlerTypes[0].HandlerType);
    }

    [Fact]
    public void Add_Event_Handler_Populates_Registrations()
    {
        var options = new HangfireOptions();

        options.Add(typeof(TestEventHandler));

        Assert.Single(options.HandlerTypes);
        Assert.Equal(typeof(TestEventHandler), options.HandlerTypes[0].HandlerType);
        Assert.Equal(typeof(TestEvent), options.HandlerTypes[0].EventType);
    }

    [Fact]
    public void Scan_Throws_After_Lock()
    {
        var options = new HangfireOptions();
        options.Add(typeof(TestHandler));

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Scan(typeof(ExecutionFlowOptionsTests).Assembly));
    }

    [Fact]
    public void Add_Throws_After_Lock()
    {
        var options = new HangfireOptions();
        options.Add(typeof(TestHandler));

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Add(typeof(TestEventHandler)));
    }

    [Fact]
    public void Configure_Makes_Registrations_Available_Via_Setup()
    {
        var setup = new HangfireSetup();

        setup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
        });

        Assert.NotEmpty(setup.RecurringHandlers);
        Assert.Contains(setup.RecurringHandlers, r => r.HandlerType == typeof(TestHandler));
    }

    // Test types

    public class TestEvent { }

    public class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
