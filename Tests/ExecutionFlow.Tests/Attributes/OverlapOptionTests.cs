using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests.Attributes;

/// <summary>012-interval-and-overlap FR-003: overlap control declared with <see cref="NoOverlapAttribute"/> or the plan entry.</summary>
public class OverlapOptionTests
{
    private class TestOptions : ExecutionFlowOptions { }

    [Fact]
    public void Registry_ExposesOverlapTimeout()
    {
        var options = new TestOptions();

        options.Add(typeof(NoOverlapHandler));
        options.Add(typeof(OverlappingHandler));

        Assert.Equal(TimeSpan.FromMinutes(10), options.RecurringHandlers[typeof(NoOverlapHandler)].OverlapTimeout);
        Assert.Null(options.RecurringHandlers[typeof(OverlappingHandler)].OverlapTimeout);
    }

    [Theory]
    [InlineData(typeof(ZeroTimeoutHandler))]
    [InlineData(typeof(NegativeTimeoutHandler))]
    public void Configure_Throws_WhenTimeoutIsNotPositive(Type handlerType)
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(handlerType));

        Assert.Contains(handlerType.FullName!, ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoOverlap_Throws_WhenTimeoutIsNotPositive(int seconds)
    {
        var planner = new ExecutionPlanner();

        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Add<OverlappingHandler>().NoOverlap(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Build_UsesAttributeOverlap_WhenEntryDoesNotSetIt()
    {
        var planner = new ExecutionPlanner();
        planner.Add<NoOverlapHandler>();
        planner.Add<OverlappingHandler>();

        var plan = planner.Build();

        Assert.Equal(TimeSpan.FromMinutes(10), plan.Get(typeof(NoOverlapHandler)).OverlapTimeout);
        Assert.Null(plan.Get(typeof(OverlappingHandler)).OverlapTimeout);
    }

    [Fact]
    public void Build_Throws_WhenAttributeTimeoutIsNotPositive()
    {
        var planner = new ExecutionPlanner();
        planner.Add(typeof(ZeroTimeoutHandler));

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Build());

        Assert.Contains(typeof(ZeroTimeoutHandler).FullName!, ex.Message);
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Recurring("*/5 * * * *"), NoOverlap(600)] private class NoOverlapHandler : TestHandler { }
    [Recurring("*/5 * * * *")] private class OverlappingHandler : TestHandler { }
    [Recurring("*/5 * * * *"), NoOverlap(0)] private abstract class ZeroTimeoutHandler : TestHandler { }
    [Recurring("*/5 * * * *"), NoOverlap(-5)] private abstract class NegativeTimeoutHandler : TestHandler { }
}
