using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests.Attributes;

/// <summary>013-dependencies-by-attribute FR-002: the run mode declared with <see cref="DependentRunAttribute"/>.</summary>
public class DependentRunAttributeTests
{
    private static ExecutionPlan Build(Type dependentType)
    {
        var planner = new ExecutionPlanner();
        planner.Add<PrerequisiteHandler>();
        planner.Add(dependentType);
        return planner.Build();
    }

    [Fact]
    public void Plan_ReadsMinInterval_FromAttribute()
    {
        var step = Build(typeof(MinIntervalHandler)).Get(typeof(MinIntervalHandler));

        Assert.Equal(TimeSpan.FromMinutes(20), step.MinInterval);
        Assert.False(step.RunsOnOwnSchedule);
    }

    [Fact]
    public void Plan_ReadsRunOnOwnSchedule_FromAttribute()
    {
        var step = Build(typeof(OwnScheduleHandler)).Get(typeof(OwnScheduleHandler));

        Assert.True(step.RunsOnOwnSchedule);
        Assert.Null(step.MinInterval);
    }

    [Fact]
    public void Plan_ReadsMinInterval_BelowOneMinute()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Build(typeof(ThirtySecondsHandler)).Get(typeof(ThirtySecondsHandler)).MinInterval);
    }

    [Theory]
    [InlineData(typeof(MinIntervalWithoutIntervalHandler))]
    [InlineData(typeof(OwnScheduleWithIntervalHandler))]
    public void Plan_Throws_WhenModeAndIntervalDisagree(Type handlerType)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(handlerType));

        Assert.Contains(handlerType.FullName!, ex.Message);
    }

    [Theory]
    [InlineData(typeof(InvalidIntervalHandler), "soon")]
    [InlineData(typeof(ZeroIntervalHandler), "00:00:00")]
    public void Plan_Throws_WhenMinIntervalIsInvalid(Type handlerType, string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(handlerType));

        Assert.Contains(handlerType.FullName!, ex.Message);
        Assert.Contains($"'{value}'", ex.Message);
    }

    [Fact]
    public void Plan_Throws_WhenHandlerHasNoPrerequisites()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NoPrerequisiteHandler>();

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Build());

        Assert.Contains(typeof(NoPrerequisiteHandler).FullName!, ex.Message);
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class PrerequisiteHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval, "00:20:00")]
    private class MinIntervalHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.OwnSchedule)]
    private class OwnScheduleHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval, "00:00:30")]
    private class ThirtySecondsHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval)]
    private abstract class MinIntervalWithoutIntervalHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.OwnSchedule, "00:20:00")]
    private abstract class OwnScheduleWithIntervalHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval, "soon")]
    private abstract class InvalidIntervalHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler)), DependentRun(DependentRunMode.MinInterval, "00:00:00")]
    private abstract class ZeroIntervalHandler : TestHandler { }

    [DependentRun(DependentRunMode.OwnSchedule)]
    private abstract class NoPrerequisiteHandler : TestHandler { }
}
