using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>005-recurring-jobs FR-009: <see cref="HangfireOptions.UsePlan"/>.</summary>
public class ExecutionPlanHangfireTests
{
    private static ExecutionPlan CreatePlan(bool brandEnabled = true)
    {
        var planner = new ExecutionPlanner();
        planner.Add<BrandHandler>().Enabled(brandEnabled).DisplayName("Brands");
        planner.Add<ProductHandler>().DependsOn<BrandHandler>().Enabled(brandEnabled);
        return planner.Build();
    }

    [Fact]
    public void UsePlan_RegistersPlanHandlers()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => o.UsePlan(CreatePlan()));

        Assert.True(setup.RecurringHandlers.ContainsKey(typeof(BrandHandler)));
        Assert.True(setup.RecurringHandlers.ContainsKey(typeof(ProductHandler)));
    }

    /// <summary>012-interval-and-overlap AC-004.2: the Hangfire behavior holds through the core options type.</summary>
    [Fact]
    public void UsePlan_ThroughBaseOptions_RegistersPlan()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => ((ExecutionFlowOptions)o).UsePlan(CreatePlan(brandEnabled: false)));

        Assert.NotNull(setup.Options.Plan);
        Assert.True(setup.RecurringHandlers.ContainsKey(typeof(ProductHandler)));
        Assert.False(RecurringJobResolver.IsAutoRun(typeof(BrandHandler), setup.Options));
    }

    [Fact]
    public void UsePlan_SetsAutoRunFromStepEnabled()
    {
        var disabled = new HangfireSetup();
        disabled.Configure(o => o.UsePlan(CreatePlan(brandEnabled: false)));

        var enabled = new HangfireSetup();
        enabled.Configure(o =>
        {
            o.GlobalRecurringAutoRun = false;
            o.UsePlan(CreatePlan());
        });

        Assert.False(RecurringJobResolver.IsAutoRun(typeof(BrandHandler), disabled.Options));
        Assert.True(RecurringJobResolver.IsAutoRun(typeof(BrandHandler), enabled.Options));
    }

    /// <summary>014-handler-configuration AC-002.6: without Enabled on the builder, SetJobAutoRun applies (it used to throw).</summary>
    [Fact]
    public void SetJobAutoRun_Applies_WhenBuilderDoesNotSetEnabled()
    {
        var planner = new ExecutionPlanner();
        planner.Add<BrandHandler>();
        var plan = planner.Build();

        var setup = new HangfireSetup();
        setup.Configure(o =>
        {
            o.SetJobAutoRun<BrandHandler>(false);
            o.UsePlan(plan);
        });

        Assert.False(RecurringJobResolver.IsAutoRun(typeof(BrandHandler), setup.Options));
    }

    /// <summary>014-handler-configuration AC-002.5: the builder's Enabled wins over SetJobAutoRun, set before or after UsePlan.</summary>
    [Fact]
    public void SetJobAutoRun_IsIgnored_WhenBuilderSetsEnabled()
    {
        var planner = new ExecutionPlanner();
        planner.Add<BrandHandler>().Enabled(true);
        var plan = planner.Build();

        var setup = new HangfireSetup();
        setup.Configure(o =>
        {
            o.UsePlan(plan);
            o.SetJobAutoRun<BrandHandler>(false);
        });

        Assert.True(RecurringJobResolver.IsAutoRun(typeof(BrandHandler), setup.Options));
    }

    [Fact]
    public void UsePlan_Throws_WhenCalledTwice()
    {
        var setup = new HangfireSetup();

        Assert.Throws<InvalidOperationException>(() => setup.Configure(o =>
        {
            o.UsePlan(CreatePlan());
            o.UsePlan(CreatePlan());
        }));
    }

    [Fact]
    public void GetName_UsesPlanDisplayName()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => o.UsePlan(CreatePlan()));
        var resolver = new JobDisplayNameResolver(new DefaultRecurringServiceIdGenerator(), setup);

        Assert.Equal("Brands", resolver.GetName(JobBuilder.CreateRecurringJob(typeof(BrandHandler))));
    }

    [Fact]
    public void GetName_NamesPostponedTrigger_AfterDependent()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => o.UsePlan(CreatePlan()));
        var idGenerator = new DefaultRecurringServiceIdGenerator();
        var job = global::Hangfire.Common.Job.FromExpression<HangfireJobDispatcher>(
            d => d.TriggerPlanDependent(null!, typeof(BrandHandler).FullName!));

        Assert.Equal("Brands (postponed trigger)", new JobDisplayNameResolver(idGenerator, setup).GetName(job));
        Assert.Equal("Brands (postponed trigger)", new DefaultHangfireJobName(idGenerator, setup).GetName(job));
    }

    [Fact]
    public void Configure_Accepts_DependentWithoutRecurringAttribute()
    {
        var planner = new ExecutionPlanner();
        planner.Add<BrandHandler>();
        planner.Add<UnscheduledHandler>().DependsOn<BrandHandler>();
        var plan = planner.Build();

        var setup = new HangfireSetup();
        setup.Configure(o => o.UsePlan(plan));

        Assert.True(setup.RecurringHandlers.ContainsKey(typeof(UnscheduledHandler)));
    }

    [Fact]
    public void Configure_Throws_WhenOwnScheduleDependentHasNoCron()
    {
        var planner = new ExecutionPlanner();
        planner.Add<BrandHandler>();
        planner.Add<UnscheduledHandler>().DependsOn<BrandHandler>().RunMode(DependentRunMode.OwnSchedule);
        var plan = planner.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new HangfireSetup().Configure(o => o.UsePlan(plan)));

        Assert.Contains(typeof(UnscheduledHandler).FullName!, ex.Message);
    }

    [Fact]
    public void Configure_Throws_WhenIndependentHandlerHasNoRecurringAttribute()
    {
        var planner = new ExecutionPlanner();
        planner.Add<UnscheduledHandler>();
        var plan = planner.Build();

        Assert.Throws<InvalidOperationException>(() => new HangfireSetup().Configure(o => o.UsePlan(plan)));
    }

    // Abstract so assembly scans in other tests skip it: a recurring handler without a schedule is invalid outside a plan.
    public abstract class UnscheduledHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("* * * * *")]
    public class BrandHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("* * * * *")]
    public class ProductHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
