using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Tests.Abstractions;

/// <summary>015-hosting-second-intervals FR-003: the interval minimum of <see cref="HandlerBuilder.Interval"/>.</summary>
public class HandlerBuilderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(-1000)]
    public void Interval_Throws_WhenBelowOneSecond(int milliseconds)
    {
        var planner = new ExecutionPlanner();

        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Add<PlainHandler>().Interval(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(90500)]
    public void Interval_Accepts_FractionsOfASecond(int milliseconds)
    {
        var planner = new ExecutionPlanner();
        planner.Add<PlainHandler>().Interval(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), planner.Build().Get<PlainHandler>().Interval);
    }

    private class PlainHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
