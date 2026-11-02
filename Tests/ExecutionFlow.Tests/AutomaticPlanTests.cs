using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests;

/// <summary>013-dependencies-by-attribute FR-001 and FR-003: the automatic plan formed from the registered handlers' attributes.</summary>
public class AutomaticPlanTests
{
    private class TestOptions : ExecutionFlowOptions { }

    private class TestSetup : ExecutionFlowSetup<TestOptions>
    {
        protected override void OnConfigured(TestOptions options) { }
    }

    private static TestSetup Configure(Action<TestOptions> configure)
    {
        var setup = new TestSetup();
        setup.Configure(configure);
        return setup;
    }

    [Fact]
    public void Options_KeepRegistrationOrder()
    {
        var options = new TestOptions();
        options.Add(typeof(DependentHandler));
        options.Add(typeof(PrerequisiteHandler));
        options.Add(typeof(DependentHandler));

        Assert.Equal(new[] { typeof(DependentHandler), typeof(PrerequisiteHandler) }, options.RecurringHandlerOrder);
    }

    [Fact]
    public void Configure_BuildsAutomaticPlan_FromDependsOnAttributes()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(DependentHandler));
            o.Add(typeof(PrerequisiteHandler));
        });

        var plan = setup.Options.Plan!;
        Assert.Equal(new[] { typeof(PrerequisiteHandler) }, plan.Get<DependentHandler>().Prerequisites);
        Assert.Equal(new[] { typeof(DependentHandler) }, plan.Get<PrerequisiteHandler>().Dependents);
    }

    [Fact]
    public void Configure_FormsNoPlan_WhenNoHandlerHasDependsOn()
    {
        var setup = Configure(o => o.Add(typeof(PrerequisiteHandler)));

        Assert.Null(setup.Options.Plan);
    }

    [Fact]
    public void Configure_FormsPlan_WhenOnlyDependentRunIsPresent()
    {
        // The plan is formed, so [DependentRun] without prerequisites fails instead of being ignored.
        var ex = Assert.Throws<InvalidOperationException>(() => Configure(o => o.Add(typeof(RunModeOnlyHandler))));

        Assert.Contains(typeof(RunModeOnlyHandler).FullName!, ex.Message);
    }

    [Fact]
    public void Configure_Throws_WithCyclePath()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Configure(o =>
        {
            o.Add(typeof(CycleAHandler));
            o.Add(typeof(CycleBHandler));
        }));

        Assert.Contains("CycleAHandler -> CycleBHandler -> CycleAHandler", ex.Message);
    }

    [Fact]
    public void Configure_Throws_WhenPrerequisiteNotRegistered()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Configure(o => o.Add(typeof(DependentHandler))));

        Assert.Contains(typeof(DependentHandler).FullName!, ex.Message);
        Assert.Contains(typeof(PrerequisiteHandler).FullName!, ex.Message);
    }

    [Fact]
    public void Configure_TurnsOffDependentsInCascade_WhenSkipDependents()
    {
        var setup = Configure(o =>
        {
            o.AutomaticPlanMissingPrerequisite = MissingPrerequisite.SkipDependents;
            o.Add(typeof(DependentHandler));
            o.Add(typeof(ChainedHandler));
        });

        var plan = setup.Options.Plan!;
        Assert.False(plan.Get<DependentHandler>().Enabled);
        Assert.Contains(nameof(PrerequisiteHandler), plan.Get<DependentHandler>().SkipReason);
        Assert.False(plan.Get<ChainedHandler>().Enabled);
    }

    [Fact]
    public void AutomaticPlanMissingPrerequisite_DefaultsToThrow_AndLocks()
    {
        var setup = Configure(o => { });

        Assert.Equal(MissingPrerequisite.Throw, setup.Options.AutomaticPlanMissingPrerequisite);
        Assert.Throws<InvalidOperationException>(() => setup.Options.AutomaticPlanMissingPrerequisite = MissingPrerequisite.SkipDependents);
    }

    [Fact]
    public void GetPlanOf_DependsOn_FormsAutomaticPlan()
    {
        var setup = Configure(o =>
        {
            o.Add(typeof(PrerequisiteHandler));
            o.Add(typeof(PlainHandler));
            o.GetPlanOf(typeof(PlainHandler)).DependsOn(typeof(PrerequisiteHandler));
        });

        Assert.Equal(new[] { typeof(PrerequisiteHandler) }, setup.Options.Plan!.Get(typeof(PlainHandler)).Prerequisites);
    }

    // Test types (abstract, so assembly scans in other tests don't pick them up)

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private abstract class PrerequisiteHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler))] private abstract class DependentHandler : TestHandler { }

    private abstract class PlainHandler : TestHandler { }

    [DependsOn(typeof(DependentHandler))] private abstract class ChainedHandler : TestHandler { }

    [DependentRun(DependentRunMode.OwnSchedule)] private abstract class RunModeOnlyHandler : TestHandler { }

    [DependsOn(typeof(CycleBHandler))] private abstract class CycleAHandler : TestHandler { }

    [DependsOn(typeof(CycleAHandler))] private abstract class CycleBHandler : TestHandler { }
}
