using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// A Hangfire state election filter that dispatches job lifecycle events (enqueued, processing, succeeded,
    /// failed, cancelled, retrying) to registered state handler instances.
    /// </summary>
    public class HangfireStateFilter : IElectStateFilter
    {
        private readonly IReadOnlyList<Type> _stateHandlers;
        private readonly IExecutionFlowRegistry _handlerRegistry;
        private readonly IServiceProvider _serviceProvider;

        public HangfireStateFilter(IExecutionFlowRegistry handlerRegistry, IServiceProvider serviceProvider, IReadOnlyList<Type> stateHandlers)
        {
            _serviceProvider = serviceProvider;
            _stateHandlers = stateHandlers;
            _handlerRegistry = handlerRegistry;
        }

        public void OnStateElection(ElectStateContext context)
        {
            var candidateState = context.CandidateState;
            var jobId = context.BackgroundJob.Id;
            var customId = GetCustomId(context, jobId);
            var handlerType = HangfireJobInfo.Create(context.BackgroundJob.Job)?.GetHandlerType(_handlerRegistry);

            if (candidateState is EnqueuedState)
            {
                if (IsRetry(context))
                {
                    var duration = GetDuration(context);
                    var attemptNumber = GetAttemptNumber(context);
                    var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, attemptNumber, duration);
                    foreach (var handler in GetAllInstancesOf<IOnRetrying>())
                        handler.OnRetrying(retryEvent);
                }
                else
                {
                    var executionEvent = new ExecutionEvent(jobId, customId, handlerType);
                    foreach (var handler in GetAllInstancesOf<IOnEnqueued>())
                        handler.OnEnqueued(executionEvent);
                }
            }
            else if (candidateState is ProcessingState)
            {
                var executionEvent = new ExecutionEvent(jobId, customId, handlerType);
                foreach (var handler in GetAllInstancesOf<IOnProcessing>())
                    handler.OnProcessing(executionEvent);
            }
            else if (candidateState is SucceededState)
            {
                var duration = GetDuration(context);
                var succeededEvent = new ExecutionSucceededEvent(jobId, customId, handlerType, duration);
                foreach (var handler in GetAllInstancesOf<IOnSucceeded>())
                    handler.OnSucceeded(succeededEvent);
            }
            else if (candidateState is FailedState failedState)
            {
                var duration = GetDuration(context);
                var failedEvent = new ExecutionFailedEvent(jobId, customId, handlerType, failedState.Exception, duration);
                foreach (var handler in GetAllInstancesOf<IOnFailed>())
                    handler.OnFailed(failedEvent);
            }
            else if (candidateState is DeletedState)
            {
                var executionEvent = new ExecutionEvent(jobId, customId, handlerType);
                foreach (var handler in GetAllInstancesOf<IOnCancelled>())
                    handler.OnCancelled(executionEvent);
            }
            else if (candidateState is ScheduledState && IsRetry(context))
            {
                var duration = GetDuration(context);
                var attemptNumber = GetAttemptNumber(context);
                var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, attemptNumber, duration);
                foreach (var handler in GetAllInstancesOf<IOnRetrying>())
                    handler.OnRetrying(retryEvent);
            }
        }

        private IEnumerable<TState> GetAllInstancesOf<TState>()
        {
            var stateType = typeof(TState);
            return _stateHandlers
                .Where(stateType.IsAssignableFrom)
                .Select(_serviceProvider.GetService)
                .Where(x => x != null)
                .Cast<TState>();
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
                () => context.Connection.GetJobParameter(jobId, ContextConsts.CustomId));
        }

        private static bool IsRetry(ElectStateContext context)
        {
            return context.CurrentState == FailedState.StateName ||
                   (context.CurrentState == ScheduledState.StateName && GetRetryCount(context) > 0);
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

        private static TimeSpan GetDuration(ElectStateContext context)
        {
            return SafeExecute("get duration", context.BackgroundJob.Id, () =>
            {
                var processingState = context.BackgroundJob.Job != null
                    ? context.Connection.GetStateData(context.BackgroundJob.Id)
                    : null;

                if (processingState?.Data != null &&
                    processingState.Data.TryGetValue(ContextConsts.StartedAt, out var startedAtStr) &&
                    DateTime.TryParse(startedAtStr, out var startedAt))
                {
                    return DateTime.UtcNow - startedAt;
                }

                return TimeSpan.Zero;
            });
        }
    }
}
