using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>Timed waits on an <see cref="IClock"/>.</summary>
    internal static class ClockExtensions
    {
        /// <summary>
        /// Waits for <paramref name="task"/> up to <paramref name="timeout"/> on the clock. Returns <c>true</c> when the task
        /// completed first. Throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        public static async Task<bool> WaitAsync(this IClock clock, Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delay = clock.Delay(timeout, delayCancellation.Token);
                var winner = await Task.WhenAny(task, delay).ConfigureAwait(false);
                delayCancellation.Cancel();

                if (winner == task)
                    return true;

                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
        }
    }
}
