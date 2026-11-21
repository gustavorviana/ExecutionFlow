using ExecutionFlow.Hosting.Infrastructure;

namespace ExecutionFlow.Hosting.Tests
{
    public class ConcurrencyLimiterTests
    {
        // AC-006.1
        [Fact]
        public async Task WaitAsync_Blocks_WhenLimitReached()
        {
            using var limiter = new ConcurrencyLimiter(2);
            await limiter.WaitAsync(CancellationToken.None);
            await limiter.WaitAsync(CancellationToken.None);

            var third = limiter.WaitAsync(CancellationToken.None);
            await Task.Delay(50);
            Assert.False(third.IsCompleted);

            limiter.Release();
            await third.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
