using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Tests.Abstractions;

public class ExecutionPlannerTests
{
    // --- REQ-001: declare handlers ---

    [Fact]
    public void Add_Throws_WhenHandlerAddedTwice()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Add<HandlerA>());

        Assert.Contains(typeof(HandlerA).FullName!, ex.Message);
    }

    [Fact]
    public void Add_Throws_WhenTypeIsNotRecurringHandler()
    {
        Assert.Throws<ArgumentException>(() => new ExecutionPlanner().Add(typeof(string)));
    }

    [Fact]
    public void Add_Throws_AfterBuild()
    {
        var planner = new ExecutionPlanner();
        var entry = planner.Add<HandlerA>();
        planner.Build();

        Assert.Throws<InvalidOperationException>(() => planner.Add<HandlerB>());
        Assert.Throws<InvalidOperationException>(() => entry.DependsOn<HandlerB>());
        Assert.Throws<InvalidOperationException>(() => entry.Enabled(false));
        Assert.Throws<InvalidOperationException>(() => planner.Build());
    }

    // --- REQ-002: declare dependencies ---

    [Fact]
    public void Build_ListsPrerequisitesAndDependents()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>().DependsOn<HandlerA>();

        var plan = planner.Build();

        Assert.Equal(new[] { typeof(HandlerA) }, plan.Get<HandlerB>().Prerequisites);
        Assert.Equal(new[] { typeof(HandlerB) }, plan.Get<HandlerA>().Dependents);
        Assert.Empty(plan.Get<HandlerA>().Prerequisites);
    }

    [Fact]
    public void Add_MergesDependsOnAttribute()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>();
        planner.Add<AttributeDependentHandler>().DependsOn<HandlerB>().DependsOn<HandlerA>();

        var plan = planner.Build();

        Assert.Equal(new[] { typeof(HandlerA), typeof(HandlerB) }, plan.Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void DependsOn_IgnoresDuplicates()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        var entry = planner.Add<HandlerB>().DependsOn<HandlerA>().DependsOn(typeof(HandlerA));

        Assert.Single(entry.Prerequisites);
    }

    [Fact]
    public void DependsOn_Throws_WhenSelfDependency()
    {
        var entry = new ExecutionPlanner().Add<HandlerA>();

        var ex = Assert.Throws<InvalidOperationException>(() => entry.DependsOn<HandlerA>());

        Assert.Contains(typeof(HandlerA).FullName!, ex.Message);
    }

    [Fact]
    public void Add_Throws_WhenAttributeIsSelfDependency()
    {
        Assert.Throws<InvalidOperationException>(() => new ExecutionPlanner().Add<SelfDependentHandler>());
    }

    // --- REQ-003: enabled handlers and display names ---

    [Fact]
    public void Build_KeepsDisabledHandler_WithReason()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>().Enabled(false);

        var step = planner.Build().Get<HandlerA>();

        Assert.False(step.Enabled);
        Assert.Equal("disabled", step.SkipReason);
    }

    [Fact]
    public void Build_ResolvesDisplayName_InPrecedenceOrder()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NamedHandler>().DisplayName("Fluent");
        planner.Add<OtherNamedHandler>();
        planner.Add<HandlerA>();

        var plan = planner.Build();

        Assert.Equal("Fluent", plan.Get<NamedHandler>().DisplayName);
        Assert.Equal("From attribute", plan.Get<OtherNamedHandler>().DisplayName);
        Assert.Equal(nameof(HandlerA), plan.Get<HandlerA>().DisplayName);
    }

    // --- REQ-004: unavailable prerequisites ---

    [Fact]
    public void Build_Throws_WhenPrerequisiteNotAdded()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerB>().DependsOn<HandlerA>();

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Build());

        Assert.Contains(typeof(HandlerB).FullName!, ex.Message);
        Assert.Contains(typeof(HandlerA).FullName!, ex.Message);
    }

    [Fact]
    public void Build_Throws_WhenPrerequisiteDisabled()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>().Enabled(false);
        planner.Add<HandlerB>().DependsOn<HandlerA>();

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Build());

        Assert.Contains("disabled", ex.Message);
    }

    [Fact]
    public void Build_DisablesDependentsInCascade_WhenSkipDependents()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>().Enabled(false);
        planner.Add<HandlerB>().DependsOn<HandlerA>();
        planner.Add<HandlerC>().DependsOn<HandlerB>();
        planner.Add<HandlerD>();

        var plan = planner.Build(MissingPrerequisite.SkipDependents);

        Assert.False(plan.Get<HandlerB>().Enabled);
        Assert.Contains(nameof(HandlerA), plan.Get<HandlerB>().SkipReason);
        Assert.False(plan.Get<HandlerC>().Enabled);
        Assert.Contains(nameof(HandlerB), plan.Get<HandlerC>().SkipReason);
        Assert.True(plan.Get<HandlerD>().Enabled);
        Assert.Null(plan.Get<HandlerD>().SkipReason);
    }

    [Fact]
    public void Build_Accepts_WhenDisabledHandlerHasUnavailablePrerequisite()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerB>().DependsOn<HandlerA>().Enabled(false);

        var step = planner.Build().Get<HandlerB>();

        Assert.Equal("disabled", step.SkipReason);
    }

    // --- REQ-005: cycles ---

    [Theory]
    [InlineData(MissingPrerequisite.Throw)]
    [InlineData(MissingPrerequisite.SkipDependents)]
    public void Build_Throws_WithCyclePath(MissingPrerequisite policy)
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>().DependsOn<HandlerC>();
        planner.Add<HandlerB>().DependsOn<HandlerA>();
        planner.Add<HandlerC>().DependsOn<HandlerB>();

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Build(policy));

        Assert.Contains("HandlerA -> HandlerC -> HandlerB -> HandlerA", ex.Message);
    }

    // --- REQ-007: run mode of a dependent ---

    [Fact]
    public void Build_ExposesRunMode()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>().DependsOn<HandlerA>().MinInterval(TimeSpan.FromMinutes(10));
        planner.Add<HandlerC>().DependsOn<HandlerA>().RunOnOwnSchedule();
        planner.Add<HandlerD>().DependsOn<HandlerA>();

        var plan = planner.Build();

        Assert.Equal(TimeSpan.FromMinutes(10), plan.Get<HandlerB>().MinInterval);
        Assert.False(plan.Get<HandlerB>().RunsOnOwnSchedule);
        Assert.True(plan.Get<HandlerC>().RunsOnOwnSchedule);
        Assert.Null(plan.Get<HandlerD>().MinInterval);
        Assert.False(plan.Get<HandlerD>().RunsOnOwnSchedule);
    }

    [Fact]
    public void MinInterval_Throws_WhenNotPositive()
    {
        var entry = new ExecutionPlanner().Add<HandlerA>();

        Assert.Throws<ArgumentOutOfRangeException>(() => entry.MinInterval(TimeSpan.Zero));
    }

    [Fact]
    public void Build_Throws_WhenMinIntervalAndOwnScheduleAreCombined()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>().DependsOn<HandlerA>().MinInterval(TimeSpan.FromMinutes(1)).RunOnOwnSchedule();

        Assert.Throws<InvalidOperationException>(() => planner.Build());
    }

    [Fact]
    public void Build_Throws_WhenRunModeIsSetWithoutPrerequisites()
    {
        var withMinInterval = new ExecutionPlanner();
        withMinInterval.Add<HandlerA>().MinInterval(TimeSpan.FromMinutes(1));

        var withOwnSchedule = new ExecutionPlanner();
        withOwnSchedule.Add<HandlerA>().RunOnOwnSchedule();

        Assert.Throws<InvalidOperationException>(() => withMinInterval.Build());
        Assert.Throws<InvalidOperationException>(() => withOwnSchedule.Build());
    }

    // --- REQ-006: order and immutability ---

    [Fact]
    public void Build_OrdersPrerequisitesFirst()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerC>().DependsOn<HandlerB>();
        planner.Add<HandlerB>().DependsOn<HandlerA>();
        planner.Add<HandlerA>();

        var order = planner.Build().Steps.Select(s => s.HandlerType);

        Assert.Equal(new[] { typeof(HandlerA), typeof(HandlerB), typeof(HandlerC) }, order);
    }

    [Fact]
    public void Build_KeepsDeclarationOrder_WhenIndependent()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerC>();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>();

        var order = planner.Build().Steps.Select(s => s.HandlerType);

        Assert.Equal(new[] { typeof(HandlerC), typeof(HandlerA), typeof(HandlerB) }, order);
    }

    [Fact]
    public void Build_ReturnsReadOnlyCollections()
    {
        var planner = new ExecutionPlanner();
        planner.Add<HandlerA>();
        planner.Add<HandlerB>().DependsOn<HandlerA>();

        var plan = planner.Build();

        Assert.Throws<NotSupportedException>(() => ((IList<ExecutionPlanStep>)plan.Steps).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Type>)plan.Get<HandlerB>().Prerequisites).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Type>)plan.Get<HandlerA>().Dependents).Clear());
    }

    [Fact]
    public void Get_Throws_WhenHandlerNotInPlan()
    {
        var plan = new ExecutionPlanner().Build();

        Assert.Throws<KeyNotFoundException>(() => plan.Get<HandlerA>());
        Assert.False(plan.TryGet(typeof(HandlerA), out _));
    }

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class HandlerA : TestHandler { }
    private class HandlerB : TestHandler { }
    private class HandlerC : TestHandler { }
    private class HandlerD : TestHandler { }

    [DependsOn(typeof(HandlerA))]
    private class AttributeDependentHandler : TestHandler { }

    [DependsOn(typeof(SelfDependentHandler))]
    private class SelfDependentHandler : TestHandler { }

    [DisplayName("From attribute")]
    private class NamedHandler : TestHandler { }

    [DisplayName("From attribute")]
    private class OtherNamedHandler : TestHandler { }
}
