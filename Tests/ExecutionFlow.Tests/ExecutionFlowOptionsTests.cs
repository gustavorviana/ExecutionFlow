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

    // --- 012-interval-and-overlap FR-004: the plan is registered on the core options ---

    private static ExecutionPlan CreatePlan()
    {
        var planner = new ExecutionPlanner();
        planner.Add<TestHandler>();
        return planner.Build();
    }

    [Fact]
    public void UsePlan_StoresPlan()
    {
        var options = new TestOptions();
        var plan = CreatePlan();

        options.UsePlan(plan);

        Assert.Same(plan, options.Plan);
    }

    [Fact]
    public void UsePlan_Throws_WhenCalledTwice()
    {
        var options = new TestOptions();
        options.UsePlan(CreatePlan());

        Assert.Throws<InvalidOperationException>(() => options.UsePlan(CreatePlan()));
    }

    [Fact]
    public void UsePlan_Throws_WhenPlanIsNull()
    {
        var options = new TestOptions();

        Assert.Throws<ArgumentNullException>(() => options.UsePlan(null!));
    }

    // --- Recurring ID format (specs/016-id-format-validation FR-004) ---

    // AC-004.2
    [Fact]
    public void AddHandler_Throws_WhenRecurringIdIsInvalid()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.Add(typeof(InvalidRecurringIdHandler)));

        Assert.Contains(typeof(InvalidRecurringIdHandler).FullName!, ex.Message);
        Assert.Contains("sync:daily", ex.Message);
    }

    [Fact]
    public void AddHandler_Throws_WhenBuilderIdIsInvalid()
    {
        var options = new TestOptions();
        options.Add(typeof(TestHandler));

        var ex = Assert.Throws<ArgumentException>(() => options.GetPlanOf<TestHandler>().Id("sync:daily"));

        Assert.Equal("id", ex.ParamName);
        Assert.Contains(typeof(TestHandler).FullName!, ex.Message);
    }

    // AC-004.4
    [Fact]
    public void AddHandler_Throws_WhenRecurringIntervalIdIsInvalid()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.Add(typeof(InvalidIntervalIdHandler)));

        Assert.Contains(typeof(InvalidIntervalIdHandler).FullName!, ex.Message);
        Assert.Contains(@"sync daily\n", ex.Message);
        Assert.DoesNotContain("\n", ex.Message);
    }

    [Fact]
    public void AddHandler_Accepts_RecurringIdWithAccents()
    {
        var options = new TestOptions();

        options.Add(typeof(AccentedIdHandler));

        Assert.Equal("sincronização-diária", options.RecurringHandlers.Values.Single().Id);
    }

    [Recurring("* * * * *", Id = "sync:daily")] private abstract class InvalidRecurringIdHandler : TestHandler { }
    [RecurringInterval("00:00:05", Id = "sync daily\n")] private abstract class InvalidIntervalIdHandler : TestHandler { }
    [Recurring("* * * * *", Id = "sincronização-diária")] private abstract class AccentedIdHandler : TestHandler { }

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
