using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Hangfire.Tests;

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

        var registration = setup.EventHandlers.Values.Single();
        Assert.Equal(typeof(OrderCreatedHandler), registration.HandlerType);
        Assert.Equal(typeof(OrderCreatedEvent), registration.EventType);
        Assert.IsNotType<RecurringJobRegistryInfo>(registration);
    }

    [Fact]
    public void Configure_Registers_RecurringHandler()
    {
        var setup = new HangfireSetup();

        setup.Configure(options =>
        {
            options.Add(typeof(CleanupHandler));
        });

        var registration = setup.RecurringHandlers.Single().Value;
        Assert.Equal(typeof(CleanupHandler), registration.HandlerType);
        Assert.IsType<RecurringJobRegistryInfo>(registration);
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

        Assert.Contains(setup.EventHandlers.Values, r => r.HandlerType == typeof(OrderCreatedHandler));
        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(CleanupHandler));
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

        Assert.Contains(setup.RecurringHandlers.Values, r => r.HandlerType == typeof(InlineHandler));
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

        var eventHandlers = setup.EventHandlers.Values.ToList();
        var recurringHandlers = setup.RecurringHandlers.Values.ToList();

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
