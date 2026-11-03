using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// Runs the occurrences of the scheduled handlers and the triggers of the plan's dependents: overlap guard, concurrency
    /// limit, execution, then the plan's generation rule. Everything it starts stops quietly when the host stops.
    /// </summary>
    internal sealed class RunCoordinator : IDisposable
    {
        private readonly HostingSetup _setup;
        private readonly HandlerExecutor _executor;
        private readonly DependencyGate _gate;
        private readonly ConcurrencyLimiter _limiter;
        private readonly OverlapGuard _overlapGuard;
        private readonly IClock _clock;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly ConcurrentDictionary<Task, byte> _inFlight = new ConcurrentDictionary<Task, byte>();
        private readonly ConcurrentDictionary<Type, DateTimeOffset> _lastEnd = new ConcurrentDictionary<Type, DateTimeOffset>();

        public RunCoordinator(HostingSetup setup, HandlerExecutor executor, DependencyGate gate, ConcurrencyLimiter limiter,
            OverlapGuard overlapGuard, IClock clock, ILogger logger)
        {
            _setup = setup;
            _executor = executor;
            _gate = gate;
            _limiter = limiter;
            _overlapGuard = overlapGuard;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>Gets the token cancelled when the host stops.</summary>
        public CancellationToken Stopping => _stopping.Token;

        /// <summary>Starts an occurrence of a scheduled handler without waiting for it (a fixed cadence may overlap).</summary>
        public void StartOccurrence(Type handlerType)
        {
            Track(RunOccurrenceAsync(handlerType));
        }

        /// <summary>
        /// Runs an occurrence of a scheduled handler. A dependent on its own schedule runs only after a new cycle of every
        /// prerequisite, otherwise the occurrence is skipped with a log line.
        /// </summary>
        public async Task RunOccurrenceAsync(Type handlerType)
        {
            if (!_setup.IsGatedByPlan(handlerType))
            {
                await RunAsync(handlerType).ConfigureAwait(false);
                return;
            }

            if (!_gate.TryBegin(handlerType, out var reason))
            {
                _logger.LogInformation("ExecutionFlow: skipped '{Handler}': {Reason}.", handlerType.FullName, reason);
                return;
            }

            try
            {
                await RunAsync(handlerType).ConfigureAwait(false);
            }
            finally
            {
                _gate.Finish(handlerType);
            }
        }

        private void Trigger(Type dependentType)
        {
            Track(RunTriggeredAsync(dependentType));
        }

        /// <summary>
        /// Runs a dependent triggered by its prerequisites. With a minimum interval, a trigger earlier than the interval after
        /// the previous run's end is postponed to the end of the interval. When new cycles completed during the run, it runs again.
        /// </summary>
        private async Task RunTriggeredAsync(Type dependentType)
        {
            var minInterval = _setup.StepOf(dependentType).MinInterval;
            do
            {
                if (minInterval.HasValue && _lastEnd.TryGetValue(dependentType, out var lastEnd))
                {
                    var wait = lastEnd + minInterval.Value - _clock.UtcNow;
                    if (wait > TimeSpan.Zero)
                    {
                        _logger.LogInformation("ExecutionFlow: '{Handler}' was triggered before its minimum interval, so it runs in {Delay}.",
                            dependentType.FullName, wait);

                        try
                        {
                            await _clock.Delay(wait, Stopping).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (Stopping.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }

                await RunAsync(dependentType).ConfigureAwait(false);
                _lastEnd[dependentType] = _clock.UtcNow;
            }
            while (!Stopping.IsCancellationRequested && _gate.Finish(dependentType));
        }

        /// <summary>One run: overlap guard, concurrency slot, execution, then the dependents it triggers. Returns whether it completed.</summary>
        private async Task<bool> RunAsync(Type handlerType)
        {
            var overlapTimeout = _setup.OverlapTimeoutOf(handlerType);
            var guarded = false;

            try
            {
                if (overlapTimeout.HasValue)
                {
                    var skip = _setup.Options.SkipWhenOverlappingOf(handlerType);
                    switch (await _overlapGuard.EnterAsync(handlerType, overlapTimeout.Value, skip, Stopping).ConfigureAwait(false))
                    {
                        case OverlapGuard.Result.Skipped:
                            _logger.LogInformation("ExecutionFlow: skipped '{Handler}': its previous run is still running.", handlerType.FullName);
                            return false;
                        case OverlapGuard.Result.TimedOut:
                            _logger.LogError("ExecutionFlow: '{Handler}' failed: it waited more than {Timeout} for its previous run to end.",
                                handlerType.FullName, overlapTimeout.Value);
                            return false;
                    }

                    guarded = true;
                }

                await _limiter.WaitAsync(Stopping).ConfigureAwait(false);
                (HandlerExecutor.Status Status, Exception Error) result;
                try
                {
                    _logger.LogDebug("ExecutionFlow: running '{Handler}'.", handlerType.FullName);
                    result = await _executor.ExecuteAsync(handlerType, Stopping).ConfigureAwait(false);
                }
                finally
                {
                    _limiter.Release();
                }

                switch (result.Status)
                {
                    case HandlerExecutor.Status.Completed:
                        foreach (var dependentType in _gate.Complete(handlerType))
                            Trigger(dependentType);
                        return true;
                    case HandlerExecutor.Status.Failed:
                        _logger.LogError(result.Error, "ExecutionFlow: '{Handler}' failed.", handlerType.FullName);
                        return false;
                    default:
                        return false;
                }
            }
            catch (OperationCanceledException) when (Stopping.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                if (guarded)
                    _overlapGuard.Release(handlerType);
            }
        }

        private void Track(Task task)
        {
            _inFlight.TryAdd(task, 0);
            task.ContinueWith(t => _inFlight.TryRemove(t, out _), TaskScheduler.Default);
        }

        /// <summary>Gets the runs and triggers in progress (for tests).</summary>
        internal int InFlightCount => _inFlight.Count;

        /// <summary>Cancels every run and pending trigger, and waits for them to end.</summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Cancel();

            var pending = Task.WhenAll(_inFlight.Keys.ToArray());
            await Task.WhenAny(pending, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        }

        /// <summary>Cancels every run and pending trigger without waiting for them.</summary>
        public void Cancel()
        {
            _stopping.Cancel();
        }

        // The token source has no timer, so it's never disposed: it may still be cancelled while the host shuts down.
        public void Dispose()
        {
            Cancel();
        }
    }
}
