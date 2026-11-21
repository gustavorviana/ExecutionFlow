using Hangfire.Server;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// Keeps runs of a recurring handler with overlap turned off from overlapping (specs/012-interval-and-overlap/spec.md,
    /// FR-003): it holds a distributed lock per recurring job for the whole run, so a second run waits for the first one and
    /// fails when the timeout passes, like Hangfire's own <c>[DisableConcurrentExecution]</c>.
    /// </summary>
    /// <remarks>
    /// The timeout comes from the execution plan's step when the handler is in the plan, else from
    /// <see cref="Attributes.NoOverlapAttribute"/>. A timeout throws in <see cref="OnPerforming"/>, so the run fails and
    /// follows the handler's retry rules.
    /// </remarks>
    internal sealed class NoOverlapFilter : IServerFilter
    {
        private const string LockItem = "ExecutionFlow.NoOverlapLock";
        private const string LockResourcePrefix = "executionflow:overlap:";

        private readonly HangfireSetup _setup;

        public NoOverlapFilter(HangfireSetup setup)
        {
            _setup = setup;
        }

        internal static string GetLockResource(string recurringJobId) => LockResourcePrefix + recurringJobId;

        public void OnPerforming(PerformingContext context)
        {
            var job = context.BackgroundJob?.Job;
            if (job == null || !job.IsRecurring())
                return;

            var handlerType = new HangfireRecurringJobInfo(job).HandlerType;
            var timeout = handlerType == null ? null : _setup.ResolveOverlapTimeout(handlerType);
            if (!timeout.HasValue)
                return;

            context.Items[LockItem] = context.Connection.AcquireDistributedLock(
                GetLockResource(_setup.ResolveRecurringJobId(handlerType)), timeout.Value);
        }

        public void OnPerformed(PerformedContext context)
        {
            if (context.Items.TryGetValue(LockItem, out var held) && held is IDisposable distributedLock)
            {
                context.Items.Remove(LockItem);
                distributedLock.Dispose();
            }
        }
    }
}
