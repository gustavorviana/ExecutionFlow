using ExecutionFlow.Hosting.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>Runs every enabled handler once: the plan's steps in order, then the handlers outside the plan in registration order.</summary>
    internal sealed class ExecutionPlanRunner : IExecutionPlanRunner
    {
        private readonly HostingSetup _setup;
        private readonly HandlerExecutor _executor;

        public ExecutionPlanRunner(HostingSetup setup, HandlerExecutor executor)
        {
            _setup = setup;
            _executor = executor;
        }

        public async Task<RunOnceResult> RunOnceAsync(CancellationToken cancellationToken)
        {
            var outcomes = new List<HandlerRunOutcome>();
            var completed = new HashSet<Type>();
            var steps = _setup.Plan?.Steps ?? Array.Empty<global::ExecutionFlow.Abstractions.ExecutionPlanStep>();

            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!step.Enabled)
                {
                    outcomes.Add(new HandlerRunOutcome(step.HandlerType, HandlerRunStatus.Skipped, step.SkipReason));
                    continue;
                }

                var missing = step.Prerequisites.FirstOrDefault(p => !completed.Contains(p));
                if (missing != null)
                {
                    outcomes.Add(new HandlerRunOutcome(step.HandlerType, HandlerRunStatus.Skipped, $"prerequisite '{missing.Name}' didn't complete"));
                    continue;
                }

                outcomes.Add(await RunAsync(step.HandlerType, completed, cancellationToken).ConfigureAwait(false));
            }

            // A handler also in the plan runs only in its plan position.
            foreach (var handlerType in _setup.RecurringOrder.Where(t => _setup.StepOf(t) == null))
            {
                cancellationToken.ThrowIfCancellationRequested();

                outcomes.Add(_setup.IsEnabled(handlerType)
                    ? await RunAsync(handlerType, completed, cancellationToken).ConfigureAwait(false)
                    : new HandlerRunOutcome(handlerType, HandlerRunStatus.Skipped, "disabled"));
            }

            return new RunOnceResult(outcomes);
        }

        private async Task<HandlerRunOutcome> RunAsync(Type handlerType, HashSet<Type> completed, CancellationToken cancellationToken)
        {
            var (status, error) = await _executor.ExecuteAsync(handlerType, cancellationToken).ConfigureAwait(false);
            switch (status)
            {
                case HandlerExecutor.Status.Completed:
                    completed.Add(handlerType);
                    return new HandlerRunOutcome(handlerType, HandlerRunStatus.Completed);
                case HandlerExecutor.Status.Failed:
                    return new HandlerRunOutcome(handlerType, HandlerRunStatus.Failed, error.Message, error);
                default:
                    throw new OperationCanceledException(cancellationToken);
            }
        }
    }
}
