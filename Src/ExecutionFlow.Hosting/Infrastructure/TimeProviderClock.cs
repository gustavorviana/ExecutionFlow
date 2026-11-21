#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>A clock over <see cref="TimeProvider"/> (<see cref="HostingOptions.TimeProvider"/>).</summary>
    internal sealed class TimeProviderClock : IClock
    {
        private readonly TimeProvider _timeProvider;

        public TimeProviderClock(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, _timeProvider, cancellationToken);
        }
    }
}
#endif
