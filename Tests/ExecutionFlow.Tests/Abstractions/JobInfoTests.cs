using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Tests.Abstractions;

public class JobInfoTests
{
    [Fact]
    public void Constructor_StoresAllProperties()
    {
        var eventType = typeof(string);
        var stateChangedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var info = new JobInfo("job-1", "custom-1", "MyEvent", eventType, false, JobState.Processing, stateChangedAt);

        Assert.Equal("job-1", info.JobId);
        Assert.Equal("custom-1", info.CustomId);
        Assert.Equal("MyEvent", info.EventTypeName);
        Assert.Equal(eventType, info.EventType);
        Assert.False(info.IsRecurring);
        Assert.Equal(JobState.Processing, info.State);
        Assert.Equal(stateChangedAt, info.StateChangedAt);
    }

    [Fact]
    public void Constructor_AllowsNullCustomId()
    {
        var info = new JobInfo("job-1", null, "MyEvent", typeof(string), false, JobState.Enqueued, null);

        Assert.Null(info.CustomId);
    }

    [Fact]
    public void Constructor_AllowsNullStateChangedAt()
    {
        var info = new JobInfo("job-1", null, null, null, false, JobState.Failed, null);

        Assert.Null(info.StateChangedAt);
        Assert.Null(info.EventTypeName);
        Assert.Null(info.EventType);
    }

    [Fact]
    public void IsRecurring_ReflectsProvidedValue()
    {
        var recurring = new JobInfo("1", null, null, null, true, JobState.Processing, null);
        var nonRecurring = new JobInfo("2", null, null, null, false, JobState.Processing, null);

        Assert.True(recurring.IsRecurring);
        Assert.False(nonRecurring.IsRecurring);
    }

    [Fact]
    public void State_ReflectsAllEnumValues()
    {
        Assert.Equal(JobState.Enqueued, new JobInfo("1", null, null, null, false, JobState.Enqueued, null).State);
        Assert.Equal(JobState.Processing, new JobInfo("1", null, null, null, false, JobState.Processing, null).State);
        Assert.Equal(JobState.Succeeded, new JobInfo("1", null, null, null, false, JobState.Succeeded, null).State);
        Assert.Equal(JobState.Failed, new JobInfo("1", null, null, null, false, JobState.Failed, null).State);
        Assert.Equal(JobState.Cancelled, new JobInfo("1", null, null, null, false, JobState.Cancelled, null).State);
    }
}
