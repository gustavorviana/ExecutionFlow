using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Diagnostics;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    /// <summary>
    /// Releases the deduplication reservation key when a job leaves the active states
    /// (succeeded, deleted or failed), in the same transaction as the state change.
    /// </summary>
    internal class DeduplicationCleanupFilter : IApplyStateFilter
    {
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (!DeduplicationStore.IsReleaseState(context.NewState?.Name))
                return;

            var jobId = context.BackgroundJob.Id;
            try
            {
                var customId = JobParameters.ReadCustomId(context.Connection, jobId);
                if (!string.IsNullOrEmpty(customId))
                    DeduplicationStore.Release(context.Connection, transaction, customId, jobId);
            }
            catch (Exception ex)
            {
                // A key left behind is detected as stale on the next publish; never fail the state change.
                Trace.TraceWarning("ExecutionFlow: Failed to release deduplication key for job '{0}': {1}", jobId, ex.Message);
            }
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }
    }
}
