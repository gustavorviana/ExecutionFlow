using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>Caps how many handler runs execute at the same time (<see cref="HostingOptions.MaxConcurrency"/>). Waiting holds no thread.</summary>
    internal sealed class ConcurrencyLimiter : IDisposable
    {
        private readonly SemaphoreSlim _slots;

        public ConcurrencyLimiter(int maxConcurrency)
        {
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
            _slots = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>Gets the number of free slots.</summary>
        public int Available => _slots.CurrentCount;

        /// <summary>Waits for a free slot.</summary>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _slots.WaitAsync(cancellationToken);
        }

        /// <summary>Frees a slot taken with <see cref="WaitAsync"/>.</summary>
        public void Release()
        {
            _slots.Release();
        }

        public void Dispose()
        {
            _slots.Dispose();
        }
    }
}
