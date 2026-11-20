using Hangfire.Client;
using Hangfire.States;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// Runs an interval with no exact cron (e.g. 7 or 90 minutes, or 45 seconds) on top of a per-minute recurring job (or a
    /// per-second one for an interval with a seconds part, specs/015-hosting-second-intervals FR-005): each scheduler
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
                || !registration.Interval.HasValue || _setup.IsTriggeredByPlan(handlerType))
                return;

            var interval = _setup.EffectiveIntervalOf(registration.Interval.Value);
            if (IntervalCron.TryGetExactCron(interval, out _))
                return;

            var isScheduleOccurrence = context.InitialState is EnqueuedState enqueued
                && string.Equals(enqueued.Reason, PrerequisiteGateFilter.SchedulerEnqueueReason, StringComparison.Ordinal);

            if (!IntervalTickerStore.TryStartRun(context.Connection, _setup.ResolveRecurringJobId(handlerType),
                    interval, _setup.UtcNow(), force: !isScheduleOccurrence,
                    resolution: IntervalCron.IsWholeMinutes(interval) ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1)))
                context.Canceled = true;
        }

        public void OnCreated(CreatedContext context)
        {
        }
    }
}
