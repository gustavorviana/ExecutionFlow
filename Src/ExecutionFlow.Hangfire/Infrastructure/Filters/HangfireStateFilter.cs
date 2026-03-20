using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
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
                    var attemptNumber = GetAttemptNumber(context);
                    var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, attemptNumber);
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
                var failedEvent = new ExecutionFailedEvent(jobId, customId, handlerType, failedState.Exception);
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
                var attemptNumber = GetAttemptNumber(context);
                var retryEvent = new ExecutionRetryingEvent(jobId, customId, handlerType, attemptNumber);
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
                .Cast<TState>();
        }

        private static string GetCustomId(ElectStateContext context, string jobId)
        {
            try
            {
                return context.Connection.GetJobParameter(jobId, HangfireDispatcher.EventId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsRetry(ElectStateContext context)
        {
            return context.CurrentState == "Failed" ||
                   (context.CurrentState == "Scheduled" && GetRetryCount(context) > 0);
        }

        private static int GetRetryCount(ElectStateContext context)
        {
            try
            {
                var retryCountStr = context.Connection.GetJobParameter(context.BackgroundJob.Id, "RetryCount");
                if (int.TryParse(retryCountStr, out var count))
                    return count;
            }
            catch (Exception)
            {
                // Ignore
            }
            return 0;
        }

        private static int GetAttemptNumber(ElectStateContext context)
        {
            var retryCount = GetRetryCount(context);
            return retryCount > 0 ? retryCount : 1;
        }

        private static TimeSpan GetDuration(ElectStateContext context)
        {
            try
            {
                var processingState = context.BackgroundJob.Job != null
                    ? context.Connection.GetStateData(context.BackgroundJob.Id)
                    : null;

                if (processingState?.Data != null &&
                    processingState.Data.TryGetValue("StartedAt", out var startedAtStr) &&
                    DateTime.TryParse(startedAtStr, out var startedAt))
                {
                    return DateTime.UtcNow - startedAt;
                }
            }
            catch
            {
                // Ignore
            }
            return TimeSpan.Zero;
        }
    }
}
