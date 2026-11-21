using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// Keeps runs of a handler whose overlap is off from running at the same time (012 FR-003, mirroring Hangfire): a new run
    /// waits for the running one up to the timeout, or, in skip mode, is skipped at once. Waiting holds no thread.
    /// </summary>
    internal sealed class OverlapGuard
    {
        private readonly ConcurrentDictionary<Type, SemaphoreSlim> _locks = new ConcurrentDictionary<Type, SemaphoreSlim>();
        private readonly IClock _clock;

        public OverlapGuard(IClock clock)
        {
            _clock = clock;
        }

        /// <summary>The result of <see cref="EnterAsync"/>.</summary>
        public enum Result
        {
            /// <summary>The run may start; release the guard when it ends.</summary>
            Entered,

            /// <summary>A run is in progress and the skip mode is on.</summary>
            Skipped,

            /// <summary>The running run outlasted the timeout.</summary>
            TimedOut,
        }

        /// <summary>
        /// Takes the handler's guard. On <see cref="Result.Entered"/>, call <see cref="Release"/> when the run ends.
        /// Throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled while waiting.
        /// </summary>
        public async Task<Result> EnterAsync(Type handlerType, TimeSpan timeout, bool skip, CancellationToken cancellationToken)
        {
            var gate = _locks.GetOrAdd(handlerType, _ => new SemaphoreSlim(1, 1));
            if (gate.Wait(0))
                return Result.Entered;

            if (skip)
                return Result.Skipped;

            using (var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var acquire = gate.WaitAsync(waitCancellation.Token);
                if (await _clock.WaitAsync(acquire, timeout, cancellationToken).ConfigureAwait(false))
                    return Result.Entered;

                waitCancellation.Cancel();
                try
                {
                    // The guard may have been taken just as the timeout expired: then the run goes ahead.
                    await acquire.ConfigureAwait(false);
                    return Result.Entered;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Result.TimedOut;
                }
            }
        }

        /// <summary>Releases the guard taken with <see cref="EnterAsync"/>.</summary>
        public void Release(Type handlerType)
        {
            if (_locks.TryGetValue(handlerType, out var gate))
                gate.Release();
        }
    }
}
