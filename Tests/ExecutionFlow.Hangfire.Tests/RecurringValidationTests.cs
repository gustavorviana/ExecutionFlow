using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>recurring-jobs AC-001.3 and REQ-008: configuration errors surface at Configure.</summary>
public class RecurringValidationTests
{
    [Fact]
    public void Configure_Throws_WhenRecurringHandlerHasNoCron()
    {
        var setup = new HangfireSetup();

        var ex = Assert.Throws<InvalidOperationException>(() => setup.Configure(o => o.Add(typeof(NoCronHandler))));

        Assert.Contains(typeof(NoCronHandler).FullName!, ex.Message);
        Assert.Contains("[Recurring", ex.Message);
    }

    [Fact]
    public void Configure_Throws_WhenGlobalTimeZoneIsUnknown()
    {
        var setup = new HangfireSetup();

        var ex = Assert.Throws<InvalidOperationException>(() => setup.Configure(o =>
        {
            o.Add(typeof(ValidHandler));
            o.RecurringTimeZone = "Not/AZone";
        }));

        Assert.Contains("Not/AZone", ex.Message);
    }

    [Fact]
    public void Configure_Throws_WhenAttributeTimeZoneIsUnknown()
    {
        var setup = new HangfireSetup();

        Assert.Throws<InvalidOperationException>(() => setup.Configure(o => o.Add(typeof(BadZoneHandler))));
    }

    [Fact]
    public void Configure_Throws_WhenOptionTimeZoneIsUnknown()
    {
        var setup = new HangfireSetup();

        Assert.Throws<InvalidOperationException>(() => setup.Configure(o =>
        {
            o.Add(typeof(ValidHandler));
            o.SetJobTimeZone<ValidHandler>("Not/AZone");
        }));
    }

    [Fact]
    public void Configure_Throws_WhenSetJobTimeZoneReferencesUnregisteredHandler()
    {
        var setup = new HangfireSetup();

        Assert.Throws<InvalidOperationException>(() => setup.Configure(o =>
        {
            o.Add(typeof(ValidHandler));
            o.SetJobTimeZone<BadZoneHandler>("America/Sao_Paulo");
        }));
    }

    [Fact]
    public void Configure_Accepts_ValidTimeZones()
    {
        var setup = new HangfireSetup();

        setup.Configure(o =>
        {
            o.Add(typeof(ValidHandler));
            o.RecurringTimeZone = "America/Sao_Paulo";
            o.SetJobTimeZone<ValidHandler>("Europe/Lisbon");
        });
    }

    [Fact]
    public void Add_CopiesIdAndTimeZoneFromAttribute_ToRegistry()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => o.Add(typeof(ExplicitHandler)));

        var registration = setup.RecurringHandlers[typeof(ExplicitHandler)];

        Assert.Equal("explicit-id", registration.Id);
        Assert.Equal("America/Sao_Paulo", registration.TimeZone);
    }

    // Abstract so assembly scans in other tests skip them; Configure only needs the registration.

    public abstract class NoCronHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 8 * * *")]
    public class ValidHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 8 * * *", TimeZone = "Not/AZone")]
    public abstract class BadZoneHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Recurring("0 8 * * *", Id = "explicit-id", TimeZone = "America/Sao_Paulo")]
    public class ExplicitHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
