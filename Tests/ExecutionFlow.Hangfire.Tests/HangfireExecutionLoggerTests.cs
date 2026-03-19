using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Tests;

public class HangfireExecutionLoggerTests
{
    [Fact]
    public void Constructor_Throws_WhenPerformContextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HangfireExecutionLogger(null!));
    }
}
