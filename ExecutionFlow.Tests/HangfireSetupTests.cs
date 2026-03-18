using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire;
using System.ComponentModel;

namespace ExecutionFlow.Tests;

public class HangfireSetupTests
{
    [Fact]
    public void Configure_Registers_EventHandler()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(OrderCreatedHandler));
        });

        var registration = setup.Registrations.Single();
        Assert.Equal(typeof(OrderCreatedHandler), registration.HandlerType);
        Assert.Equal(typeof(OrderCreatedEvent), registration.EventType);
        Assert.False(registration.IsRecurring);
    }

    [Fact]
    public void Configure_Registers_RecurringHandler()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(CleanupHandler));
        });

        var registration = setup.Registrations.Single();
        Assert.Equal(typeof(CleanupHandler), registration.HandlerType);
        Assert.True(registration.IsRecurring);
        Assert.Equal("0 * * * *", registration.Cron);
        Assert.Equal("Cleanup Old Records", registration.DisplayName);
    }

    [Fact]
    public void Configure_Scan_Finds_All_Handlers()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Scan(typeof(HangfireSetupTests).Assembly);
        });

        Assert.Contains(setup.Registrations, r => r.HandlerType == typeof(OrderCreatedHandler));
        Assert.Contains(setup.Registrations, r => r.HandlerType == typeof(CleanupHandler));
    }

    [Fact]
    public void Configure_Can_Mix_Scan_And_Add()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Scan(typeof(HangfireSetupTests).Assembly);
            options.Add(typeof(InlineHandler));
        });

        Assert.Contains(setup.Registrations, r => r.HandlerType == typeof(InlineHandler));
    }

    [Fact]
    public void Registrations_Distinguishes_Recurring_From_Event()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(OrderCreatedHandler));
            options.Add(typeof(CleanupHandler));
        });

        var eventHandlers = setup.Registrations.Where(r => !r.IsRecurring).ToList();
        var recurringHandlers = setup.Registrations.Where(r => r.IsRecurring).ToList();

        Assert.Single(eventHandlers);
        Assert.Single(recurringHandlers);
        Assert.Equal(typeof(OrderCreatedHandler), eventHandlers[0].HandlerType);
        Assert.Equal(typeof(CleanupHandler), recurringHandlers[0].HandlerType);
    }

    // --- Test types ---

    public class OrderCreatedEvent { }

    public class OrderCreatedHandler : IHandler<OrderCreatedEvent>
    {
        public Task HandleAsync(FlowContext<OrderCreatedEvent> context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Recurring("0 * * * *")]
    [DisplayName("Cleanup Old Records")]
    public class CleanupHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    public class InlineHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }
}