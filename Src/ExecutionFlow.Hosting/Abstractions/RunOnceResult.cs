using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hosting.Abstractions
{
    /// <summary>The result of <see cref="IExecutionPlanRunner.RunOnceAsync"/>: one outcome per handler, in run order.</summary>
    public sealed class RunOnceResult
    {
        /// <summary>Gets the outcome of each handler, in the order it was run or skipped.</summary>
        public IReadOnlyList<HandlerRunOutcome> Outcomes { get; }

        /// <summary>Gets whether any handler failed.</summary>
        public bool HasFailures => Outcomes.Any(o => o.Status == HandlerRunStatus.Failed);

        internal RunOnceResult(IReadOnlyList<HandlerRunOutcome> outcomes)
        {
            Outcomes = outcomes;
        }
    }
}
