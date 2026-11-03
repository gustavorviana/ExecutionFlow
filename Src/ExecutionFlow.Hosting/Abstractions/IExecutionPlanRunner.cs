using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Abstractions
{
    /// <summary>Runs every recurring handler of the local host once, in dependency order.</summary>
    public interface IExecutionPlanRunner
    {
        /// <summary>
        /// Runs each enabled handler once, one after the other: the plan's steps in order, then the handlers outside the plan
        /// in registration order. A handler is skipped when it's disabled or a prerequisite didn't complete in this run; a
        /// failure doesn't stop the handlers that don't depend on it.
        /// </summary>
        /// <param name="cancellationToken">Stops the run.</param>
        /// <returns>The outcome of each handler.</returns>
        Task<RunOnceResult> RunOnceAsync(CancellationToken cancellationToken);
    }
}
