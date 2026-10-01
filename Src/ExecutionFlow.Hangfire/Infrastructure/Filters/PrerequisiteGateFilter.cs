using Hangfire.States;
using System;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// Enforces the execution plan when a new job of a dependent recurring handler is about to be enqueued (ADR-0009).
    /// A job the plan triggered, or an occurrence of a dependent's own schedule, runs only if every prerequisite completed a
    /// new cycle; otherwise it goes to <see cref="PrerequisitesNotMetState"/>. A manual run skips the gate.
    /// </summary>
    /// <remarks>
    /// Gating at enqueue time means a gated run never takes a worker, never ends as a false Succeeded, and fires no hook.
    /// Registered with <see cref="FilterOrder"/> so <see cref="HangfireStateFilter"/> already sees the final candidate.
    /// </remarks>
    internal sealed class PrerequisiteGateFilter : IElectStateFilter
    {
        /// <summary>Before <see cref="HangfireStateFilter.FilterOrder"/>.</summary>
        public const int FilterOrder = HangfireStateFilter.FilterOrder - 10;

        /// <summary>
        /// The enqueue reason Hangfire's recurring job scheduler uses (1.8.x). It's the only thing that tells a schedule
        /// occurrence from a manual trigger. If it ever changes, manual runs of <c>RunOnOwnSchedule</c> dependents are gated
        /// like occurrences, which is the safe side.
        /// </summary>
        internal const string SchedulerEnqueueReason = "Triggered by recurring job scheduler";

        private readonly HangfireSetup _setup;

        public PrerequisiteGateFilter(HangfireSetup setup)
        {
            _setup = setup;
        }

        public void OnStateElection(ElectStateContext context)
        {
            // Only the job's creation is gated (no current state yet): a retry or requeue already consumed its generations.
            if (!(context.CandidateState is EnqueuedState candidate) || context.CurrentState != null)
                return;

            var plan = _setup.Options.Plan;
            var job = context.BackgroundJob?.Job;
            if (plan == null || job == null || !job.IsRecurring())
                return;

            var handlerType = new HangfireRecurringJobInfo(job).HandlerType;
            if (handlerType == null || !plan.TryGet(handlerType, out var step) || step.Prerequisites.Count == 0)
                return;

            var isScheduleOccurrence = step.RunsOnOwnSchedule &&
                string.Equals(candidate.Reason, SchedulerEnqueueReason, StringComparison.Ordinal);

            var allowed = PrerequisiteGenerationStore.OnDependentCreated(
                context.Connection,
                _setup.ResolveRecurringJobId(handlerType),
                step.Prerequisites.Select(_setup.ResolveRecurringJobId).ToList(),
                context.BackgroundJob.Id,
                isScheduleOccurrence,
                out _,
                out var pending);

            if (!allowed)
                context.CandidateState = new PrerequisitesNotMetState(pending);
        }
    }
}
