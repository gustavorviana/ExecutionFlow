using ExecutionFlow.Hosting;
using ExecutionFlow.Hosting.Infrastructure;
using System.Diagnostics;

namespace ExecutionFlow.Hosting.NetStandard.Tests
{
    public class SystemClockTests
    {
        [Fact]
        public void HostingOptions_HasNoTimeProvider_OnNetStandardAsset()
        {
            // Proves this project runs against the netstandard2.0 build, where only the system clock exists.
            Assert.Null(typeof(HostingOptions).GetProperty("TimeProvider"));
        }

        [Fact]
        public async Task DelayAndWait_CompleteAndHonorCancellation()
        {
            var clock = SystemClock.Instance;

            var before = clock.UtcNow;
            var watch = Stopwatch.StartNew();
            await clock.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
            Assert.True(watch.ElapsedMilliseconds >= 90);
            Assert.True(clock.UtcNow >= before.AddMilliseconds(90));

            // A negative delay completes at once.
            await clock.Delay(TimeSpan.FromSeconds(-1), CancellationToken.None);

            // A timed wait: false when the timeout wins, true when the task wins.
            var never = new TaskCompletionSource();
            Assert.False(await clock.WaitAsync(never.Task, TimeSpan.FromMilliseconds(50), CancellationToken.None));
            Assert.True(await clock.WaitAsync(Task.CompletedTask, TimeSpan.FromSeconds(30), CancellationToken.None));

            // Cancellation ends both the delay and the timed wait.
            using var cancelled = new CancellationTokenSource();
            cancelled.CancelAfter(50);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clock.Delay(TimeSpan.FromMinutes(5), cancelled.Token));

            using var cancelledWait = new CancellationTokenSource();
            cancelledWait.CancelAfter(50);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clock.WaitAsync(never.Task, TimeSpan.FromMinutes(5), cancelledWait.Token));
        }
    }
}
