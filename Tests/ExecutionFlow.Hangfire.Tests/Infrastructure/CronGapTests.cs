using ExecutionFlow.Hangfire.Infrastructure;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

/// <summary>015-hosting-second-intervals FR-002, research R-007: the smallest gap between occurrences of a cron.</summary>
public class CronGapTests
{
    [Theory]
    [InlineData("*/5 * * * * *", 5)]
    [InlineData("0,5 * * * * *", 5)]
    [InlineData("*/30 * * * * *", 30)]
    [InlineData("* * * * * *", 1)]
    [InlineData("*/15 0 * * * *", 15)]
    [InlineData("10-20/5 * * * * *", 5)]
    [InlineData("30 * * * * *", 60)]
    [InlineData("* * * * *", 60)]
    [InlineData("*/5 * * * *", 300)]
    [InlineData("0 * * * *", 3600)]
    [InlineData("0 8 * * *", 3600)]
    public void MinimumGap_ReturnsSmallestGap(string cron, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), CronGap.MinimumGap(cron));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cron")]
    [InlineData("* * *")]
    [InlineData("? * * * * *")]
    [InlineData("70 * * * * *")]
    [InlineData("*/0 * * * * *")]
    public void MinimumGap_ReturnsNull_WhenCronCannotBeRead(string? cron)
    {
        Assert.Null(CronGap.MinimumGap(cron!));
    }
}
