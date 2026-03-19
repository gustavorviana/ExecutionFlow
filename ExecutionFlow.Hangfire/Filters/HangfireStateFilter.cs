using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Filters
{
    public class HangfireStateFilter : IElectStateFilter
    {
        private readonly IReadOnlyList<Type> _stateHandlers;
        private readonly JobActivator _activator;

        public HangfireStateFilter(JobActivator activator, IReadOnlyList<Type> stateHandlers)
        {
            _activator = activator;
            _stateHandlers = stateHandlers;
        }

        public void OnStateElection(ElectStateContext context)
        {
            var candidateState = context.CandidateState;
            var jobId = context.BackgroundJob.Id;
            var customId = GetCustomId(context, jobId);
            var handlerType = GetHandlerType(context.BackgroundJob.Job);

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
                .Where(x => x.IsAssignableFrom(stateType))
                .Select(_activator.ActivateJob)
                .Cast<TState>();
        }

        private static string GetCustomId(ElectStateContext context, string jobId)
        {
            try
            {
                return context.Connection.GetJobParameter(jobId, HangfireDispatcher.EventId);
            }
            catch
            {
                return null;
            }
        }

        private static Type GetHandlerType(Job job)
        {
            if (job?.Args == null) return null;

            foreach (var arg in job.Args)
            {
                if (arg is Type type)
                    return type;
            }

            var handlerTypeName = job.Args
                .OfType<string>()
                .LastOrDefault();

            if (string.IsNullOrEmpty(handlerTypeName)) return null;

            try
            {
                return Type.GetType(handlerTypeName);
            }
            catch
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
            catch
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
