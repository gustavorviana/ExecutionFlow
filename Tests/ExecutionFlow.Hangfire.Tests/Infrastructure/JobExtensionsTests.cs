using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire.Common;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class JobExtensionsTests
{
    [Fact]
    public void IsRecurring_ReturnsTrue_ForRecurringJob()
    {
        var job = JobBuilder.CreateRecurringJob(typeof(TestHandler));

        Assert.True(job.IsRecurring());
    }

    [Fact]
    public void IsRecurring_ReturnsFalse_ForEventJob()
    {
        var job = JobBuilder.CreateEventJob(new TestEvent());

        Assert.False(job.IsRecurring());
    }

    [Fact]
    public void IsRecurring_ReturnsFalse_ForNullJob()
    {
        Job? job = null;

        Assert.False(job!.IsRecurring());
    }

    public class TestEvent { }

    public class TestHandler : ExecutionFlow.Abstractions.IHandler
    {
        public Task HandleAsync(ExecutionFlow.Abstractions.FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
