using ExecutionFlow.Abstractions.Events;
using Hangfire.Common;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Filters
{
    public class HangfireStateFilter : IElectStateFilter
    {
        private readonly IReadOnlyList<IOnEnqueued> _onEnqueued;
        private readonly IReadOnlyList<IOnProcessing> _onProcessing;
        private readonly IReadOnlyList<IOnSucceeded> _onSucceeded;
        private readonly IReadOnlyList<IOnFailed> _onFailed;
        private readonly IReadOnlyList<IOnCancelled> _onCancelled;
        private readonly IReadOnlyList<IOnRetrying> _onRetrying;

        public HangfireStateFilter(
            IReadOnlyList<IOnEnqueued> onEnqueued,
            IReadOnlyList<IOnProcessing> onProcessing,
            IReadOnlyList<IOnSucceeded> onSucceeded,
            IReadOnlyList<IOnFailed> onFailed,
            IReadOnlyList<IOnCancelled> onCancelled,
            IReadOnlyList<IOnRetrying> onRetrying)
        {
            _onEnqueued = onEnqueued ?? Array.Empty<IOnEnqueued>();
            _onProcessing = onProcessing ?? Array.Empty<IOnProcessing>();
            _onSucceeded = onSucceeded ?? Array.Empty<IOnSucceeded>();
            _onFailed = onFailed ?? Array.Empty<IOnFailed>();
            _onCancelled = onCancelled ?? Array.Empty<IOnCancelled>();
            _onRetrying = onRetrying ?? Array.Empty<IOnRetrying>();
        }

        public void OnStateElection(ElectStateContext context)
        {
            var candidateState = context.CandidateState;
            var jobId = context.BackgroundJob.Id;
            var displayName = GetDisplayName(context.BackgroundJob.Job);
            var customId = GetCustomId(context, jobId);
            var handlerType = GetHandlerType(context.BackgroundJob.Job);

            if (candidateState is EnqueuedState)
            {
                if (IsRetry(context))
                {
                    var attemptNumber = GetAttemptNumber(context);
                    var retryEvent = new ExecutionRetryingEvent(jobId, displayName, customId, handlerType, attemptNumber);
                    foreach (var handler in _onRetrying)
                        handler.OnRetrying(retryEvent);
                }
                else
                {
                    var executionEvent = new ExecutionEvent(jobId, displayName, customId, handlerType);
                    foreach (var handler in _onEnqueued)
                        handler.OnEnqueued(executionEvent);
                }
            }
            else if (candidateState is ProcessingState)
            {
                var executionEvent = new ExecutionEvent(jobId, displayName, customId, handlerType);
                foreach (var handler in _onProcessing)
                    handler.OnProcessing(executionEvent);
            }
            else if (candidateState is SucceededState)
            {
                var duration = GetDuration(context);
                var succeededEvent = new ExecutionSucceededEvent(jobId, displayName, customId, handlerType, duration);
                foreach (var handler in _onSucceeded)
                    handler.OnSucceeded(succeededEvent);
            }
            else if (candidateState is FailedState failedState)
            {
                var failedEvent = new ExecutionFailedEvent(jobId, displayName, customId, handlerType, failedState.Exception);
                foreach (var handler in _onFailed)
                    handler.OnFailed(failedEvent);
            }
            else if (candidateState is DeletedState)
            {
                var executionEvent = new ExecutionEvent(jobId, displayName, customId, handlerType);
                foreach (var handler in _onCancelled)
                    handler.OnCancelled(executionEvent);
            }
            else if (candidateState is ScheduledState && IsRetry(context))
            {
                var attemptNumber = GetAttemptNumber(context);
                var retryEvent = new ExecutionRetryingEvent(jobId, displayName, customId, handlerType, attemptNumber);
                foreach (var handler in _onRetrying)
                    handler.OnRetrying(retryEvent);
            }
        }

        private static string GetDisplayName(Job job)
        {
            if (job == null) return string.Empty;
            return job.ToString();
        }

        private static string GetCustomId(ElectStateContext context, string jobId)
        {
            try
            {
                return context.Connection.GetJobParameter(jobId, "customId");
            }
            catch
            {
                return null;
            }
        }

        private static Type GetHandlerType(Job job)
        {
            if (job?.Args == null) return null;

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
