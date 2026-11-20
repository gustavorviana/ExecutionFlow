using ExecutionFlow.Hangfire.Infrastructure;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

/// <summary>012 FR-002 and 015 FR-005: the cron registered for an interval.</summary>
public class IntervalCronTests
{
    [Theory]
    [InlineData(1, "* * * * * *")]
    [InlineData(15, "*/15 * * * * *")]
    [InlineData(30, "*/30 * * * * *")]
    [InlineData(45, IntervalCron.SecondTicker)]
    [InlineData(90, IntervalCron.SecondTicker)]
    [InlineData(60, "* * * * *")]
    [InlineData(7 * 60, IntervalCron.Ticker)]
    [InlineData(15 * 60, "*/15 * * * *")]
    [InlineData(90 * 60, IntervalCron.Ticker)]
    [InlineData(6 * 3600, "0 */6 * * *")]
    public void ToCron_ReturnsExpectedCron(int seconds, string expected)
    {
        Assert.Equal(expected, IntervalCron.ToCron(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void TryGetExactCron_ReturnsFalse_ForFractionsOfASecond()
    {
        Assert.False(IntervalCron.TryGetExactCron(TimeSpan.FromMilliseconds(1500), out _));
    }
}
