using Hangfire.Client;
using Hangfire.States;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// Runs an interval with no exact cron (e.g. 7 or 90 minutes) on top of a per-minute recurring job: each scheduler
    /// occurrence is cancelled before the job is created until the interval has elapsed since the last run
    /// (specs/012-interval-and-overlap/spec.md, FR-002). A manual trigger always runs, and counts as the last run.
    /// </summary>
    /// <remarks>
    /// Cancelling at creation means a skipped occurrence leaves no job, so nothing shows in the dashboard.
    /// </remarks>
    internal sealed class IntervalTickerFilter : IClientFilter
    {
        private readonly HangfireSetup _setup;

        public IntervalTickerFilter(HangfireSetup setup)
        {
            _setup = setup;
        }

        public void OnCreating(CreatingContext context)
        {
            var job = context.Job;
            if (job == null || !job.IsRecurring())
                return;

            var handlerType = new HangfireRecurringJobInfo(job).HandlerType;
            if (handlerType == null || !_setup.RecurringHandlers.TryGetValue(handlerType, out var registration)
                || !registration.Interval.HasValue || IntervalCron.TryGetExactCron(registration.Interval.Value, out _)
                || _setup.IsTriggeredByPlan(handlerType))
                return;

            var isScheduleOccurrence = context.InitialState is EnqueuedState enqueued
                && string.Equals(enqueued.Reason, PrerequisiteGateFilter.SchedulerEnqueueReason, StringComparison.Ordinal);

            if (!IntervalTickerStore.TryStartRun(context.Connection, _setup.ResolveRecurringJobId(handlerType),
                    registration.Interval.Value, _setup.UtcNow(), force: !isScheduleOccurrence))
                context.Canceled = true;
        }

        public void OnCreated(CreatedContext context)
        {
        }
    }
}
