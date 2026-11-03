using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests.Attributes;

/// <summary>012-interval-and-overlap FR-001: interval schedules declared with <see cref="RecurringIntervalAttribute"/>.</summary>
public class IntervalScheduleTests
{
    private class TestOptions : ExecutionFlowOptions { }

    [Fact]
    public void Registry_ExposesInterval()
    {
        var options = new TestOptions();

        options.Add(typeof(FifteenMinutesHandler));

        var info = options.RecurringHandlers[typeof(FifteenMinutesHandler)];
        Assert.Equal(TimeSpan.FromMinutes(15), info.Interval);
        Assert.Null(info.Cron);
    }

    [Fact]
    public void Registry_ExposesNoInterval_ForCronHandler()
    {
        var options = new TestOptions();

        options.Add(typeof(CronHandler));

        Assert.Null(options.RecurringHandlers[typeof(CronHandler)].Interval);
    }

    [Fact]
    public void Registry_AcceptsDaysInInterval()
    {
        var options = new TestOptions();

        options.Add(typeof(OneDayHandler));

        Assert.Equal(TimeSpan.FromDays(1), options.RecurringHandlers[typeof(OneDayHandler)].Interval);
    }

    [Fact]
    public void Registry_ResolvesId_LikeRecurring()
    {
        var options = new TestOptions();

        options.Add(typeof(IdHandler));
        options.Add(typeof(FifteenMinutesHandler));

        Assert.Equal("price-sync", options.RecurringHandlers[typeof(IdHandler)].Id);
        Assert.Null(options.RecurringHandlers[typeof(FifteenMinutesHandler)].Id);
    }

    [Fact]
    public void Configure_Throws_WhenCronAndIntervalAreBothSet()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(typeof(BothHandler)));

        Assert.Contains(typeof(BothHandler).FullName!, ex.Message);
    }

    [Theory]
    [InlineData(typeof(ThirtySecondsHandler))]
    [InlineData(typeof(NegativeHandler))]
    [InlineData(typeof(ZeroHandler))]
    public void Configure_Throws_WhenIntervalBelowOneMinute(Type handlerType)
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(handlerType));

        Assert.Contains(handlerType.FullName!, ex.Message);
    }

    [Theory]
    [InlineData(typeof(InvalidHandler), "every 5 minutes")]
    [InlineData(typeof(BareNumberHandler), "10")]
    [InlineData(typeof(EmptyHandler), "")]
    public void Configure_Throws_WhenIntervalIsInvalid(Type handlerType, string value)
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(handlerType));

        Assert.Contains(handlerType.FullName!, ex.Message);
        Assert.Contains($"'{value}'", ex.Message);
    }

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [RecurringInterval("00:15:00")] private class FifteenMinutesHandler : TestHandler { }
    [RecurringInterval("1.00:00:00")] private class OneDayHandler : TestHandler { }
    [RecurringInterval("00:15:00", Id = "price-sync")] private class IdHandler : TestHandler { }
    [Recurring("*/5 * * * *")] private class CronHandler : TestHandler { }
    [Recurring("*/5 * * * *"), RecurringInterval("00:15:00")] private abstract class BothHandler : TestHandler { }
    [RecurringInterval("00:00:30")] private abstract class ThirtySecondsHandler : TestHandler { }
    [RecurringInterval("-00:15:00")] private abstract class NegativeHandler : TestHandler { }
    [RecurringInterval("00:00:00")] private abstract class ZeroHandler : TestHandler { }
    [RecurringInterval("every 5 minutes")] private abstract class InvalidHandler : TestHandler { }
    [RecurringInterval("10")] private abstract class BareNumberHandler : TestHandler { }
    [RecurringInterval("")] private abstract class EmptyHandler : TestHandler { }
}
