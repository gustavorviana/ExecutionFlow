using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using NSubstitute;
using System.ComponentModel;

namespace ExecutionFlow.Tests;

public class ExecutionFlowOptionsTests
{
    private class TestOptions : ExecutionFlowOptions { }

    private class TestSetup : ExecutionFlowSetup<TestOptions>
    {
        protected override void OnConfigured(TestOptions options) { }
    }

    [Fact]
    public void Scan_Populates_Registrations()
    {
        var options = new TestOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly);

        Assert.True(options.EventHandlers.Count > 0 || options.RecurringHandlers.Count > 0);
    }

    [Fact]
    public void Add_RecurringHandler_Populates_RecurringHandlers()
    {
        var options = new TestOptions();

        options.Add(typeof(TestHandler));

        Assert.Single(options.RecurringHandlers);
        Assert.Equal(typeof(TestHandler), options.RecurringHandlers.Values.Single().HandlerType);
    }

    [Fact]
    public void Add_EventHandler_Populates_EventHandlers()
    {
        var options = new TestOptions();

        options.Add(typeof(TestEventHandler));

        Assert.Single(options.EventHandlers);
        Assert.Equal(typeof(TestEventHandler), options.EventHandlers.Values.Single().HandlerType);
        Assert.Equal(typeof(TestEvent), options.EventHandlers.Values.Single().EventType);
    }

    [Fact]
    public void Scan_Throws_After_Lock()
    {
        var options = new TestOptions();
        options.Add(typeof(TestHandler));

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Scan(typeof(ExecutionFlowOptionsTests).Assembly));
    }

    [Fact]
    public void Add_Throws_After_Lock()
    {
        var options = new TestOptions();
        options.Add(typeof(TestHandler));

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() => options.Add(typeof(TestEventHandler)));
    }

    [Fact]
    public void Configure_Makes_Registrations_Available_Via_Setup()
    {
        var setup = new TestSetup();

        setup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
        });

        Assert.NotEmpty(setup.RecurringHandlers);
        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(TestHandler));
    }

    [Fact]
    public void Configure_Separates_EventHandlers_From_RecurringHandlers()
    {
        var setup = new TestSetup();

        setup.Configure(opts =>
        {
            opts.Add(typeof(TestHandler));
            opts.Add(typeof(TestEventHandler));
        });

        Assert.Single(setup.EventHandlers);
        Assert.Contains(setup.EventHandlers.Values, r => r.HandlerType == typeof(TestEventHandler));
        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(TestHandler));
    }

    // --- Scan with predicate ---

    [Fact]
    public void Scan_WithPredicate_OnlyRegistersMatchingTypes()
    {
        var options = new TestOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly, type => type == typeof(TestEventHandler));

        Assert.Single(options.EventHandlers);
        Assert.Empty(options.RecurringHandlers);
    }

    [Fact]
    public void Scan_WithPredicate_ExcludesNonMatchingTypes()
    {
        var options = new TestOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly, type => type.Namespace?.Contains("DoesNotExist") == true);

        Assert.Empty(options.EventHandlers);
        Assert.Empty(options.RecurringHandlers);
    }

    [Fact]
    public void Scan_WithNullPredicate_RegistersAll()
    {
        var options = new TestOptions();

        options.Scan(typeof(ExecutionFlowOptionsTests).Assembly, null);

        Assert.True(options.EventHandlers.Count > 0 || options.RecurringHandlers.Count > 0);
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
