using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>The system clock, used where <c>TimeProvider</c> doesn't exist (the <c>netstandard2.0</c> build).</summary>
    internal sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken);
        }
    }
}
