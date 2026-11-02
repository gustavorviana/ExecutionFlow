using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Common;
using System.ComponentModel;
using System.Diagnostics;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>014-handler-configuration: the builder's values in Hangfire, precedence over options and attributes, extensions.</summary>
public class HandlerConfigurationHangfireTests
{
    private static ExecutionPlan Plan(Action<ExecutionPlanner> declare, MissingPrerequisite policy = MissingPrerequisite.Throw)
    {
        var planner = new ExecutionPlanner();
        declare(planner);
        return planner.Build(policy);
    }

    private static HangfireSetup Configure(Action<HangfireOptions> configure)
    {
        var setup = new HangfireSetup();
        setup.Configure(configure);
        return setup;
    }

    private static List<string> CaptureTrace(Action action)
    {
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            action();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        lock (listener.Messages)
            return listener.Messages.ToList();
    }

    // --- FR-001 ---

    [Fact]
    public void Build_RegistersBuilderValues()
    {
        var plan = Plan(p => p.Add<CronHandler>().Interval(TimeSpan.FromMinutes(90)).Id("configured"));

        var setup = Configure(o => o.UsePlan(plan));
        var registration = setup.RecurringHandlers[typeof(CronHandler)];

        Assert.Equal("configured", RecurringJobResolver.ResolveId(registration, new DefaultRecurringServiceIdGenerator()));
        Assert.Equal(TimeSpan.FromMinutes(90), registration.Interval);
        Assert.Null(registration.Cron);
        Assert.Equal(IntervalCron.Ticker, RecurringJobResolver.ResolveCron(registration));
    }

    [Fact]
    public void TimeZone_Throws_WhenUnknown()
    {
        var plan = Plan(p => p.Add<CronHandler>().TimeZone("Not/AZone"));

        var ex = Assert.Throws<InvalidOperationException>(() => Configure(o => o.UsePlan(plan)));

        Assert.Contains("Not/AZone", ex.Message);
    }

    // --- FR-002 ---

    [Fact]
    public void Builder_WinsOverPerHandlerOption_WithWarning()
    {
        var plan = Plan(p => p.Add<CronHandler>().Enabled(true).TimeZone("America/Sao_Paulo"));
        HangfireSetup? setup = null;

        var messages = CaptureTrace(() => setup = Configure(o =>
        {
            o.UsePlan(plan);
            o.GetPlanOf<CronHandler>().Enabled(false).TimeZone("Europe/Lisbon");
        }));

        Assert.True(RecurringJobResolver.IsAutoRun(typeof(CronHandler), setup!.Options));
        Assert.Equal("America/Sao_Paulo", RecurringJobResolver.ResolveTimeZoneId(setup.RecurringHandlers[typeof(CronHandler)], setup.Options));
        Assert.Single(messages, m => m.Contains(typeof(CronHandler).FullName!) && m.Contains("GetPlanOf"));
    }

    // --- FR-006: internal planner and the options layer ---

    [Fact]
    public void GetPlanOf_AppliesWithoutUserPlan()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(CronHandler));
            o.GetPlanOf<CronHandler>().Enabled(false).TimeZone("America/Sao_Paulo");
        });

        Assert.Null(setup.Options.Plan);
        Assert.False(RecurringJobResolver.IsAutoRun(typeof(CronHandler), setup.Options));
        Assert.Equal("America/Sao_Paulo", RecurringJobResolver.ResolveTimeZoneId(setup.RecurringHandlers[typeof(CronHandler)], setup.Options));
    }

    [Fact]
    public void UserPlan_ReplacesGetPlanOf_AsAWhole()
    {
        var plan = Plan(p => p.Add<CronHandler>());
        HangfireSetup? setup = null;

        var messages = CaptureTrace(() => setup = Configure(o =>
        {
            o.GetPlanOf<CronHandler>().Enabled(false).TimeZone("Europe/Lisbon");
            o.UsePlan(plan);
        }));

        // The user plan doesn't set Enabled or TimeZone, and still the options layer doesn't count.
        Assert.True(RecurringJobResolver.IsAutoRun(typeof(CronHandler), setup!.Options));
        Assert.Null(RecurringJobResolver.ResolveTimeZoneId(setup.RecurringHandlers[typeof(CronHandler)], setup.Options));
        Assert.Single(messages, m => m.Contains(typeof(CronHandler).FullName!) && m.Contains("GetPlanOf"));
    }

    [Fact]
    public void UserPlan_KeepsAttributes_PerItem()
    {
        var plan = Plan(p =>
        {
            p.Add<CronHandler>();
            p.Add<DecoratedHandler>().DependsOn<CronHandler>().RunMode(DependentRunMode.OwnSchedule);
        });

        var setup = Configure(o => o.UsePlan(plan));
        var registration = setup.RecurringHandlers[typeof(DecoratedHandler)];

        Assert.Equal("*/10 * * * *", registration.Cron);
        Assert.Equal("a", RecurringJobResolver.ResolveId(registration, new DefaultRecurringServiceIdGenerator()));
        Assert.Equal("Europe/Lisbon", RecurringJobResolver.ResolveTimeZoneId(registration, setup.Options));
    }

#pragma warning disable CS0618 // AC-006.4 checks the obsolete shortcuts.
    [Fact]
    public void ObsoleteOptions_RouteToGetPlanOf()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(CronHandler));
            o.SetJobAutoRun<CronHandler>(false);
            o.SetJobTimeZone<CronHandler>("America/Sao_Paulo");
        });

        Assert.False(RecurringJobResolver.IsAutoRun(typeof(CronHandler), setup.Options));
        Assert.Equal("America/Sao_Paulo", RecurringJobResolver.ResolveTimeZoneId(setup.RecurringHandlers[typeof(CronHandler)], setup.Options));
    }
#pragma warning restore CS0618

    [Fact]
    public void Builder_WinsOverAttribute_ForIdTimeZoneAndName()
    {
        var plan = Plan(p => p.Add<DecoratedHandler>().Id("b").TimeZone("America/Sao_Paulo").DisplayName("Builder"));

        var setup = Configure(o => o.UsePlan(plan));
        var registration = setup.RecurringHandlers[typeof(DecoratedHandler)];

        Assert.Equal("b", RecurringJobResolver.ResolveId(registration, new DefaultRecurringServiceIdGenerator()));
        Assert.Equal("America/Sao_Paulo", RecurringJobResolver.ResolveTimeZoneId(registration, setup.Options));
        Assert.Equal("Builder", registration.DisplayName);
        Assert.Equal("Builder", setup.Options.Plan!.Get<DecoratedHandler>().DisplayName);
    }

    [Fact]
    public void AllowOverlap_DoesNotLetDependentOverlap()
    {
        var plan = Plan(p =>
        {
            p.Add<CronHandler>();
            p.Add<DependentHandler>().DependsOn<CronHandler>().AllowOverlap();
        });

        var setup = Configure(o => o.UsePlan(plan));

        // AllowOverlap only drops the NoOverlap lock; a dependent stays triggered by the plan, which never re-triggers a running one (005 FR-012).
        Assert.Null(setup.ResolveOverlapTimeout(typeof(DependentHandler)));
        Assert.True(setup.IsTriggeredByPlan(typeof(DependentHandler)));
    }

    // --- FR-004 ---

    private static List<JobFilter> Filters(HangfireSetup setup, Type handlerType)
    {
        return new HandlerJobFilterProvider(setup, setup.Options).GetFilters(JobBuilder.CreateRecurringJob(handlerType)).ToList();
    }

    [Fact]
    public void Queue_WinsOverAttribute()
    {
        var plan = Plan(p => p.Add<DecoratedHandler>().Queue("reports"));

        var queues = Filters(Configure(o => o.UsePlan(plan)), typeof(DecoratedHandler))
            .Select(f => f.Instance).OfType<QueueAttribute>().ToList();

        Assert.Equal("reports", Assert.Single(queues).Queue);
    }

    [Fact]
    public void Retry_WinsOverAttributeAndOption()
    {
        var withAttribute = Plan(p => p.Add<DecoratedHandler>().AutomaticRetry(5, 10, 20));
        var withOption = Plan(p => p.Add<CronHandler>().AutomaticRetry(3));

        var attributeRetries = Filters(Configure(o => o.UsePlan(withAttribute)), typeof(DecoratedHandler))
            .Select(f => f.Instance).OfType<AutomaticRetryAttribute>().ToList();
        var optionRetries = Filters(Configure(o => { o.DisableRecurringRetries = true; o.UsePlan(withOption); }), typeof(CronHandler))
            .Select(f => f.Instance).OfType<AutomaticRetryAttribute>().ToList();

        Assert.Equal(5, Assert.Single(attributeRetries).Attempts);
        Assert.Equal(new[] { 10, 20 }, attributeRetries[0].DelaysInSeconds);
        Assert.Equal(3, Assert.Single(optionRetries).Attempts);
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("*/10 * * * *")] private class CronHandler : TestHandler { }
    [Recurring("*/5 * * * *")] private class DependentHandler : TestHandler { }

    [Recurring("*/10 * * * *", Id = "a", TimeZone = "Europe/Lisbon"), DisplayName("Attr"), Queue("critical"), AutomaticRetry(Attempts = 1)]
    private class DecoratedHandler : TestHandler { }

    private sealed class CollectingTraceListener : TraceListener
    {
        public List<string> Messages { get; } = new();

        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        private void Add(string? message)
        {
            lock (Messages)
                Messages.Add(message ?? string.Empty);
        }
    }
}
