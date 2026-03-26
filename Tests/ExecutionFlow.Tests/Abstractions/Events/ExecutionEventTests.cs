using ExecutionFlow.Abstractions.Events;

namespace ExecutionFlow.Tests.Abstractions.Events;

public class ExecutionEventTests
{
    [Fact]
    public void ExecutionEvent_StoresProperties()
    {
        var handlerType = typeof(ExecutionEventTests);
        var evt = new ExecutionEvent("job-1", "custom-1", handlerType);

        Assert.Equal("job-1", evt.JobId);
        Assert.Equal("custom-1", evt.CustomId);
        Assert.Equal(handlerType, evt.HandlerType);
    }

    [Fact]
    public void ExecutionEvent_CustomId_CanBeNull()
    {
        var evt = new ExecutionEvent("job-1", null!, typeof(object));

        Assert.Null(evt.CustomId);
    }

    [Fact]
    public void ExecutionSucceededEvent_StoresDuration()
    {
        var duration = TimeSpan.FromSeconds(5.5);
        var evt = new ExecutionSucceededEvent("job-1", "custom-1", typeof(object), duration);

        Assert.Equal("job-1", evt.JobId);
        Assert.Equal("custom-1", evt.CustomId);
        Assert.Equal(duration, evt.Duration);
    }

    [Fact]
    public void ExecutionFailedEvent_StoresException()
    {
        var exception = new InvalidOperationException("test error");
        var evt = new ExecutionFailedEvent("job-1", "custom-1", typeof(object), exception);

        Assert.Equal("job-1", evt.JobId);
        Assert.Equal(exception, evt.Exception);
        Assert.Equal("test error", evt.Exception.Message);
    }

    [Fact]
    public void ExecutionRetryingEvent_StoresAttemptNumber()
    {
        var evt = new ExecutionRetryingEvent("job-1", "custom-1", typeof(object), 3);

        Assert.Equal("job-1", evt.JobId);
        Assert.Equal(3, evt.AttemptNumber);
    }

    // --- Duration on base ExecutionEvent ---

    [Fact]
    public void ExecutionEvent_Duration_DefaultsToZero()
    {
        var evt = new ExecutionEvent("job-1", null, typeof(object));

        Assert.Equal(TimeSpan.Zero, evt.Duration);
    }

    [Fact]
    public void ExecutionEvent_Duration_CanBeSet()
    {
        var duration = TimeSpan.FromMinutes(2);
        var evt = new ExecutionEvent("job-1", null, typeof(object), duration);

        Assert.Equal(duration, evt.Duration);
    }

    [Fact]
    public void ExecutionFailedEvent_StoresDuration()
    {
        var duration = TimeSpan.FromSeconds(10);
        var evt = new ExecutionFailedEvent("job-1", null, typeof(object), new Exception(), duration);

        Assert.Equal(duration, evt.Duration);
    }

    [Fact]
    public void ExecutionRetryingEvent_StoresDuration()
    {
        var duration = TimeSpan.FromSeconds(30);
        var evt = new ExecutionRetryingEvent("job-1", null, typeof(object), 2, duration);

        Assert.Equal(duration, evt.Duration);
    }
}
