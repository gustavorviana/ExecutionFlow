using ExecutionFlow.Hangfire.Infrastructure.Filters;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure.Filters;

public class AutoStartNotAllowedCanceledStateTests
{
    [Fact]
    public void Name_ReturnsStateName()
    {
        var state = new AutoStartNotAllowedCanceledState();

        Assert.Equal("auto-start-not-allowed-canceled", state.Name);
    }

    [Fact]
    public void Reason_ReturnsMessage()
    {
        var state = new AutoStartNotAllowedCanceledState();

        Assert.Contains("not permitted", state.Reason);
    }

    [Fact]
    public void IsFinal_ReturnsTrue()
    {
        var state = new AutoStartNotAllowedCanceledState();

        Assert.True(state.IsFinal);
    }

    [Fact]
    public void IgnoreJobLoadException_ReturnsFalse()
    {
        var state = new AutoStartNotAllowedCanceledState();

        Assert.False(state.IgnoreJobLoadException);
    }

    [Fact]
    public void SerializeData_ContainsReason()
    {
        var state = new AutoStartNotAllowedCanceledState();

        var data = state.SerializeData();

        Assert.True(data.ContainsKey("Reason"));
        Assert.Contains("not permitted", data["Reason"]);
    }
}
