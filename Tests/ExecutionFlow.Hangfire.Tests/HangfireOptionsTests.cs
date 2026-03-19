using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;

namespace ExecutionFlow.Hangfire.Tests;

public class HangfireOptionsTests
{
    [Fact]
    public void AutoRunRecurring_DefaultsToTrue()
    {
        var options = new HangfireOptions();

        Assert.True(options.AutoRunRecurring);
    }

    [Fact]
    public void RemoveOrphanRecurringJobs_DefaultsToFalse()
    {
        var options = new HangfireOptions();

        Assert.False(options.RemoveOrphanRecurringJobs);
    }

    [Fact]
    public void SetJobAutoRun_Generic_DoesNotThrow()
    {
        var options = new HangfireOptions();

        var exception = Record.Exception(() => options.SetJobAutoRun<TestRecurringHandler>(false));

        Assert.Null(exception);
    }

    [Fact]
    public void SetJobAutoRun_Type_DoesNotThrow()
    {
        var options = new HangfireOptions();

        var exception = Record.Exception(() => options.SetJobAutoRun(typeof(TestRecurringHandler), true));

        Assert.Null(exception);
    }

    [Fact]
    public void AddStateHandler_Generic_AddsType()
    {
        var options = new HangfireOptions();

        options.AddStateHandler<TestStateHandler>();

        Assert.Single(options.StateHandlerTypes);
        Assert.Equal(typeof(TestStateHandler), options.StateHandlerTypes[0]);
    }

    [Fact]
    public void AddStateHandler_Type_AddsType()
    {
        var options = new HangfireOptions();

        options.AddStateHandler(typeof(TestStateHandler));

        Assert.Single(options.StateHandlerTypes);
        Assert.Equal(typeof(TestStateHandler), options.StateHandlerTypes[0]);
    }

    [Fact]
    public void SetJobAutoRun_Throws_After_Lock()
    {
        var options = new HangfireOptions();

        var lockMethod = typeof(ExecutionFlowOptions).GetMethod("Lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        lockMethod!.Invoke(options, null);

        Assert.Throws<InvalidOperationException>(() =>
            options.SetJobAutoRun<TestRecurringHandler>(false));
    }

    [Fact]
    public void Configure_Makes_Registrations_Available_Via_Setup()
    {
        var setup = new HangfireSetup();

        setup.Configure(opts =>
        {
            opts.Add(typeof(TestRecurringHandler));
        });

        Assert.NotEmpty(setup.RecurringHandlers);
        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(TestRecurringHandler));
    }

    [Fact]
    public void SetJobAutoRun_InvalidHandler_Throws_OnConfigure()
    {
        var setup = new HangfireSetup();

        Assert.Throws<InvalidOperationException>(() =>
        {
            setup.Configure(opts =>
            {
                opts.Add(typeof(TestRecurringHandler));
                opts.SetJobAutoRun<UnregisteredHandler>(false);
            });
        });
    }

    // Test types

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class UnregisteredHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class TestStateHandler : IOnSucceeded
    {
        public void OnSucceeded(ExecutionSucceededEvent e) { }
    }
}
