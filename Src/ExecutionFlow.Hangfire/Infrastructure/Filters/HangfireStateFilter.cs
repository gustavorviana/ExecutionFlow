using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// A Hangfire state election filter that dispatches job lifecycle events (enqueued, processing, succeeded,
    /// failed, cancelled, retrying) to registered state handler instances.
    /// </summary>
    /// <remarks>
    /// Register it with <see cref="FilterOrder"/> so it runs after Hangfire's <c>AutomaticRetryAttribute</c> (order 20)
    /// and sees the final decision: a candidate <see cref="FailedState"/> is then a final failure, and a retry shows up as a
    /// <see cref="ScheduledState"/> with the failed state in <see cref="ElectStateContext.TraversedStates"/>.
    /// </remarks>
    public class HangfireStateFilter : IElectStateFilter
    {
        /// <summary>The filter order used by <see cref="HangfireSetup"/>: after the retry filter, before continuations.</summary>
        public const int FilterOrder = 100;

        private readonly IReadOnlyList<Type> _stateHandlers;
        private readonly IExecutionFlowRegistry _handlerRegistry;
        private readonly IServiceProvider _serviceProvider;
        private readonly Action<HookErrorContext> _hookErrorHandler;

        public HangfireStateFilter(IExecutionFlowRegistry handlerRegistry, IServiceProvider serviceProvider, IReadOnlyList<Type> stateHandlers)
            : this(handlerRegistry, serviceProvider, stateHandlers, null)
        {
        }

        public HangfireStateFilter(IExecutionFlowRegistry handlerRegistry, IServiceProvider serviceProvider, IReadOnlyList<Type> stateHandlers, Action<HookErrorContext> hookErrorHandler)
        {
            _serviceProvider = serviceProvider;
            _stateHandlers = stateHandlers;
            _handlerRegistry = handlerRegistry;
            _hookErrorHandler = hookErrorHandler;
        }

        public void OnStateElection(ElectStateContext context)
        {
            var candidateState = context.CandidateState;
            var jobId = context.BackgroundJob.Id;
            var customId = GetCustomId(context, jobId);
            var handlerType = HangfireJobInfo.Create(context.BackgroundJob.Job)?.GetHandlerType(_handlerRegistry);
            var traversedFailure = context.TraversedStates?.OfType<FailedState>().LastOrDefault();

            if (candidateState is FailedState failedState)
            {
                var failedEvent = new ExecutionFailedEvent(jobId, customId, handlerType, failedState.Exception, GetDuration(context));
                Invoke<IOnFailed>(failedEvent, (h, e) => h.OnFailed(e));
            }
            else if (candidateState is DeletedState && traversedFailure != null)
            {
                // The retry filter gave up and deletes the job (OnAttemptsExceeded = Delete): a final failure.
                var failedEvent = new ExecutionFailedEvent(jobId, customId, handlerType, traversedFailure.Exception, GetDuration(context));
                Invoke<IOnFailed>(failedEvent, (h, e) => h.OnFailed(e));
            }
            else if (candidateState is DeletedState)
            {
                Invoke<IOnCancelled>(new ExecutionEvent(jobId, customId, handlerType), (h, e) => h.OnCancelled(e));
            }
            else if (candidateState is ScheduledState && traversedFailure != null)
            {
                // A failed attempt that the retry filter scheduled to run again.
                var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, GetAttemptNumber(context), traversedFailure.Exception, GetDuration(context));
                Invoke<IOnRetrying>(retryEvent, (h, e) => h.OnRetrying(e));
            }
            else if (candidateState is EnqueuedState && context.CurrentState == FailedState.StateName)
            {
                // A failed job requeued manually (IExecutionManager.Retry or the dashboard).
                var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, GetAttemptNumber(context));
                Invoke<IOnRetrying>(retryEvent, (h, e) => h.OnRetrying(e));
            }
            else if (candidateState is EnqueuedState && context.CurrentState == ScheduledState.StateName && GetRetryCount(context) > 0)
            {
                // A scheduled retry becoming due: OnRetrying already fired when it was scheduled.
            }
            else if (candidateState is EnqueuedState)
            {
                Invoke<IOnEnqueued>(new ExecutionEvent(jobId, customId, handlerType), (h, e) => h.OnEnqueued(e));
            }
            else if (candidateState is ProcessingState)
            {
                Invoke<IOnProcessing>(new ExecutionEvent(jobId, customId, handlerType), (h, e) => h.OnProcessing(e));
            }
            else if (candidateState is SucceededState)
            {
                var succeededEvent = new ExecutionSucceededEvent(jobId, customId, handlerType, GetDuration(context));
                Invoke<IOnSucceeded>(succeededEvent, (h, e) => h.OnSucceeded(e));
            }
        }

        /// <summary>
        /// Resolves and calls each registered hook for <typeparamref name="THook"/> in isolation:
        /// an exception in one hook is reported and never reaches Hangfire or the other hooks.
        /// </summary>
        private void Invoke<THook>(ExecutionEvent executionEvent, Action<THook, ExecutionEvent> call) where THook : class
        {
            var hookInterface = typeof(THook);
            foreach (var hookType in _stateHandlers.Where(hookInterface.IsAssignableFrom))
            {
                try
                {
                    if (_serviceProvider.GetService(hookType) is THook hook)
                        call(hook, executionEvent);
                }
                catch (Exception ex)
                {
                    ReportHookError(new HookErrorContext(hookType, hookInterface, executionEvent, ex));
                }
            }
        }

        private void Invoke<THook>(ExecutionFailedEvent e, Action<THook, ExecutionFailedEvent> call) where THook : class
            => Invoke<THook>((ExecutionEvent)e, (h, ev) => call(h, (ExecutionFailedEvent)ev));

        private void Invoke<THook>(ExecutionRetryingEvent e, Action<THook, ExecutionRetryingEvent> call) where THook : class
            => Invoke<THook>((ExecutionEvent)e, (h, ev) => call(h, (ExecutionRetryingEvent)ev));

        private void Invoke<THook>(ExecutionSucceededEvent e, Action<THook, ExecutionSucceededEvent> call) where THook : class
            => Invoke<THook>((ExecutionEvent)e, (h, ev) => call(h, (ExecutionSucceededEvent)ev));

        private void ReportHookError(HookErrorContext error)
        {
            if (_hookErrorHandler != null)
            {
                try
                {
                    _hookErrorHandler(error);
                    return;
                }
                catch (Exception handlerException)
                {
                    Trace.TraceWarning("ExecutionFlow: HookErrorHandler threw while reporting a hook error: {0}", handlerException.Message);
                }
            }

            Trace.TraceWarning("ExecutionFlow: Hook '{0}' ({1}) failed for job '{2}': {3}",
                error.HookType.FullName, error.HookInterface.Name, error.Event.JobId, error.Exception);
        }

        private static T SafeExecute<T>(string operation, string jobId, Func<T> action, T defaultValue = default)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ExecutionFlow: Failed to {0} for job '{1}': {2}", operation, jobId, ex.Message);
                return defaultValue;
            }
        }

        private static string GetCustomId(ElectStateContext context, string jobId)
        {
            return SafeExecute("get custom ID", jobId,
                () => JobParameters.ReadCustomId(context.Connection, jobId));
        }

        private static int GetRetryCount(ElectStateContext context)
        {
            return SafeExecute("get retry count", context.BackgroundJob.Id, () =>
            {
                var retryCountStr = context.Connection.GetJobParameter(context.BackgroundJob.Id, ContextConsts.RetryCount);
                return int.TryParse(retryCountStr, out var count) ? count : 0;
            });
        }

        private static int GetAttemptNumber(ElectStateContext context)
        {
            var retryCount = GetRetryCount(context);
            return retryCount > 0 ? retryCount : 1;
        }

        /// <summary>
        /// Time since the current Processing state started. Hangfire stores <c>StartedAt</c> in UTC ("o" format),
        /// so it's parsed as UTC: a plain <see cref="DateTime.TryParse(string, out DateTime)"/> would convert it to local time.
        /// </summary>
        private static TimeSpan GetDuration(ElectStateContext context)
        {
            return SafeExecute("get duration", context.BackgroundJob.Id, () =>
            {
                var processingState = context.BackgroundJob.Job != null
                    ? context.Connection.GetStateData(context.BackgroundJob.Id)
                    : null;

                if (processingState?.Data != null &&
                    processingState.Data.TryGetValue(ContextConsts.StartedAt, out var startedAtStr) &&
                    DateTime.TryParse(startedAtStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var startedAt))
                {
                    return DateTime.UtcNow - startedAt;
                }

                return TimeSpan.Zero;
            });
        }
    }
}
