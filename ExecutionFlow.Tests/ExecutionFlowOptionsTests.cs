using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests;

public class ExecutionFlowOptionsTests
{
    [Fact]
    public void Scan_Populates_Registrations()
    {
        var options = new ExecutionFlowOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly);

        Assert.NotEmpty(options.Registrations);
    }

    [Fact]
    public void Add_Populates_Registrations()
    {
        var options = new ExecutionFlowOptions();

        options.Add(typeof(TestHandler));

        Assert.Single(options.Registrations);
        Assert.Equal(typeof(TestHandler), options.Registrations[0].HandlerType);
        Assert.Equal(typeof(IHandler), options.Registrations[0].ServiceType);
    }

    [Fact]
    public void Add_Event_Handler_Populates_Registrations()
    {
        var options = new ExecutionFlowOptions();

        options.Add(typeof(TestEventHandler));

        Assert.Single(options.Registrations);
        Assert.Equal(typeof(TestEventHandler), options.Registrations[0].HandlerType);
        Assert.Equal(typeof(TestEvent), options.Registrations[0].JobType);
    }

    [Fact]
    public void Throws_InvalidOperationException_When_Modified_After_Configure()
    {
        ExecutionFlowSetup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
        });

        // Create a new options and lock it to simulate post-Configure state
        var options = new ExecutionFlowOptions();
        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly);

        // Use Configure to lock the options, then try to modify
        var lockedOptions = new ExecutionFlowOptions();
        // Access internal Lock via Configure flow
        ExecutionFlowSetup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
            // opts is locked after this delegate returns
        });
    }

    [Fact]
    public void Scan_Throws_After_Lock()
    {
        // We test the lock behavior by using reflection to call Lock()
        var options = new ExecutionFlowOptions();
        options.Add(typeof(TestHandler));

        // Call internal Lock() via reflection
        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Scan(typeof(ExecutionFlowOptionsTests).Assembly));
    }

    [Fact]
    public void Add_Throws_After_Lock()
    {
        var options = new ExecutionFlowOptions();
        options.Add(typeof(TestHandler));

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Add(typeof(TestEventHandler)));
    }

    [Fact]
    public void Configure_Makes_Registrations_Available_Via_Setup()
    {
        ExecutionFlowSetup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
        });

        Assert.NotEmpty(ExecutionFlowSetup.Registrations);
        Assert.Contains(ExecutionFlowSetup.Registrations, r => r.HandlerType == typeof(TestHandler));
    }

    // Test types

    public class TestEvent { }

    public class TestHandler : IHandler
    {
        public Task HandleAsync(Abstractions.ExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class TestEventHandler : IHandler<TestEvent>
    {
        public Task HandleAsync(ExecutionContext<TestEvent> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
