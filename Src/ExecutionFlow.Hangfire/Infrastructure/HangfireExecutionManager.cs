using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Manages Hangfire job execution state, providing methods to query, cancel, and retry
    /// both event jobs (by Hangfire ID or custom ID) and recurring jobs (by handler type).
    /// </summary>
    public class HangfireExecutionManager : IExecutionManager
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly JobStorage _jobStorage;

        public HangfireExecutionManager(IBackgroundJobClient jobClient, JobStorage jobStorage)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        }

        /// <summary>
        /// Determines whether an event job with the specified ID is currently being processed.
        /// Matches against the Hangfire job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to search for (Hangfire ID or custom ID).</param>
        /// <returns><c>true</c> if a matching job is processing; otherwise, <c>false</c>.</returns>
        public bool IsRunning(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return false;

            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
                return IsReservedJobInState(connection, jobId, ProcessingState.StateName) || InfraUtils
                    .ReadAll(monitoringApi.ProcessingJobs)
                    .Any(x => MatchesId(connection, x.Key, jobId));
        }

        /// <summary>
        /// Determines whether a recurring job with the specified handler type is currently being processed.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is processing; otherwise, <c>false</c>.</returns>
        public bool IsRunning(Type handlerType)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            return InfraUtils
                .ReadAll(monitoringApi.ProcessingJobs)
                .Any(x => x.Value.Job.IsRecurringOfType(handlerType));
        }

        /// <summary>
        /// Determines whether an event job with the specified ID is enqueued and waiting to be processed.
        /// Matches against the Hangfire job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to search for (Hangfire ID or custom ID).</param>
        /// <returns><c>true</c> if a matching job is enqueued; otherwise, <c>false</c>.</returns>
        public bool IsPending(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return false;

            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
                return IsReservedJobInState(connection, jobId, EnqueuedState.StateName) || monitoringApi.Queues()
                    .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                    .Any(x => MatchesId(connection, x.Key, jobId));
        }

        /// <summary>
        /// Determines whether a recurring job with the specified handler type is enqueued and waiting to be processed.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is enqueued; otherwise, <c>false</c>.</returns>
        public bool IsPending(Type handlerType)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();

            return queues
                .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                .Any(x => x.Value.Job.IsRecurringOfType(handlerType));
        }

        /// <summary>
        /// Cancels (deletes) a scheduled, enqueued or processing event job that matches the specified ID.
        /// Matches against the Hangfire job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to cancel (Hangfire ID or custom ID).</param>
        /// <returns><c>true</c> if a job was found and deleted; otherwise, <c>false</c>.</returns>
        public bool Cancel(string jobId)
        {
            var hangfireJobId = FindActiveJobId(jobId);
            return hangfireJobId != null && _jobClient.Delete(hangfireJobId);
        }

        /// <summary>
        /// Cancels (deletes) a scheduled, enqueued or processing recurring job that matches the specified handler type.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the job to cancel.</param>
        /// <returns><c>true</c> if a job was found and deleted; otherwise, <c>false</c>.</returns>
        public bool Cancel(Type handlerType)
        {
            var jobId = FindRecurringJobId(handlerType);
            return jobId != null && _jobClient.Delete(jobId);
        }

        /// <summary>
        /// Retries a failed event job that matches the specified ID by re-enqueuing it.
        /// Matches against the Hangfire job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to retry (Hangfire ID or custom ID).</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        public bool Retry(string jobId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                var failedJobId = InfraUtils
                    .ReadAll(monitoringApi.FailedJobs)
                    .FirstOrDefault(x => MatchesId(connection, x.Key, jobId))
                    .Key;

                if (string.IsNullOrEmpty(failedJobId))
                    return false;

                return _jobClient.Requeue(failedJobId);
            }
        }

        /// <summary>
        /// Retries a failed recurring job that matches the specified handler type by re-enqueuing it.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the failed job to retry.</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        public bool Retry(Type handlerType)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            var failedJobId = InfraUtils
                .ReadAll(monitoringApi.FailedJobs)
                .FirstOrDefault(x => x.Value.Job.IsRecurringOfType(handlerType))
                .Key;

            if (string.IsNullOrEmpty(failedJobId))
                return false;

            return _jobClient.Requeue(failedJobId);
        }

        /// <summary>
        /// Fast path: when <paramref name="customId"/> has a deduplication reservation key, checks the reserved job's state
        /// without scanning. A miss falls back to the scan, which also covers jobs published without deduplication.
        /// </summary>
        private static bool IsReservedJobInState(IStorageConnection connection, string customId, string stateName)
        {
            var reservedJobId = DeduplicationStore.GetReservedJobId(connection, customId);
            return reservedJobId != null && DeduplicationStore.IsInState(connection, reservedJobId, stateName);
        }

        /// <summary>
        /// Finds an active (scheduled, enqueued, awaiting or processing) job for <paramref name="jobId"/>:
        /// an exact Hangfire job ID first (no scan), then the deduplication reservation key, then a scan of
        /// Processing, each queue and Scheduled matching the Hangfire ID or custom ID.
        /// </summary>
        private string FindActiveJobId(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return null;

            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                if (IsActiveJob(connection, jobId))
                    return jobId;

                var reservedJobId = DeduplicationStore.GetReservedJobId(connection, jobId);
                if (reservedJobId != null && IsActiveJob(connection, reservedJobId))
                    return reservedJobId;

                var processingId = InfraUtils
                    .ReadAll(monitoringApi.ProcessingJobs)
                    .FirstOrDefault(x => MatchesId(connection, x.Key, jobId))
                    .Key;
                if (!string.IsNullOrEmpty(processingId))
                    return processingId;

                var enqueuedId = monitoringApi.Queues()
                    .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                    .FirstOrDefault(x => MatchesId(connection, x.Key, jobId))
                    .Key;
                if (!string.IsNullOrEmpty(enqueuedId))
                    return enqueuedId;

                return InfraUtils
                    .ReadAll(monitoringApi.ScheduledJobs)
                    .FirstOrDefault(x => MatchesId(connection, x.Key, jobId))
                    .Key;
            }
        }

        private static bool IsActiveJob(IStorageConnection connection, string jobId)
        {
            try
            {
                return DeduplicationStore.IsActive(connection, jobId);
            }
            catch (Exception ex)
            {
                // Some storages throw for IDs that aren't in their job ID format (e.g. a custom ID).
                Trace.TraceWarning("ExecutionFlow: Failed to read the state of job '{0}': {1}", jobId, ex.Message);
                return false;
            }
        }

        private string FindRecurringJobId(Type handlerType)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            var processingId = InfraUtils
                .ReadAll(monitoringApi.ProcessingJobs)
                .FirstOrDefault(x => x.Value.Job.IsRecurringOfType(handlerType))
                .Key;
            if (!string.IsNullOrEmpty(processingId))
                return processingId;

            var enqueuedId = monitoringApi.Queues()
                .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                .FirstOrDefault(x => x.Value.Job.IsRecurringOfType(handlerType))
                .Key;
            if (!string.IsNullOrEmpty(enqueuedId))
                return enqueuedId;

            return InfraUtils
                .ReadAll(monitoringApi.ScheduledJobs)
                .FirstOrDefault(x => x.Value.Job.IsRecurringOfType(handlerType))
                .Key;
        }

        /// <summary>
        /// Checks if a Hangfire job matches the given ID. Compares the Hangfire job ID first,
        /// then falls back to the custom ID stored as a job parameter.
        /// </summary>
        private static bool MatchesId(IStorageConnection connection, string hangfireJobId, string id)
        {
            // An empty ID never matches; otherwise it would match every job without a custom ID.
            if (string.IsNullOrEmpty(id))
                return false;

            if (hangfireJobId == id)
                return true;

            return GetCustomId(connection, hangfireJobId) == id;
        }

        /// <summary>
        /// Enumerates the ExecutionFlow jobs in the specified state, lazily (see <see cref="IExecutionManager.GetJobs"/>).
        /// </summary>
        /// <param name="state">The job state to filter by.</param>
        /// <returns>A lazily evaluated sequence of <see cref="JobInfo"/>.</returns>
        public IEnumerable<JobInfo> GetJobs(JobState state)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                foreach (var row in ReadRows(monitoringApi, state))
                {
                    if (IsExecutionFlowJob(row.Job, row.InvocationData))
                        yield return BuildJobInfo(connection, row.JobId, row.Job, state, row.Timestamp);
                }
            }
        }

        private static IEnumerable<(string JobId, Job Job, InvocationData InvocationData, DateTime? Timestamp)> ReadRows(IMonitoringApi monitoringApi, JobState state)
        {
            switch (state)
            {
                case JobState.Enqueued:
                    return monitoringApi.Queues()
                        .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, x.Value.EnqueuedAt));
                case JobState.Processing:
                    return InfraUtils.ReadAll(monitoringApi.ProcessingJobs)
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, x.Value.StartedAt));
                case JobState.Succeeded:
                    return InfraUtils.ReadAll(monitoringApi.SucceededJobs)
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, x.Value.SucceededAt));
                case JobState.Failed:
                    return InfraUtils.ReadAll(monitoringApi.FailedJobs)
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, x.Value.FailedAt));
                case JobState.Cancelled:
                    return InfraUtils.ReadAll(monitoringApi.DeletedJobs)
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, x.Value.DeletedAt));
                case JobState.Scheduled:
                    return InfraUtils.ReadAll(monitoringApi.ScheduledJobs)
                        .Select(x => (x.Key, x.Value.Job, x.Value.InvocationData, (DateTime?)x.Value.ScheduledAt));
                default:
                    return Enumerable.Empty<(string, Job, InvocationData, DateTime?)>();
            }
        }

        /// <summary>
        /// A loaded job is an ExecutionFlow job when it runs <see cref="HangfireJobDispatcher"/>. A job that can't be loaded
        /// (e.g. its event type no longer exists) is recognized by the dispatcher type recorded in its invocation data.
        /// </summary>
        private static bool IsExecutionFlowJob(Job job, InvocationData invocationData)
        {
            if (job != null)
                return job.IsEvent() || job.IsRecurring();

            return invocationData?.Type != null
                && invocationData.Type.StartsWith(typeof(HangfireJobDispatcher).FullName + ",", StringComparison.Ordinal);
        }

        private static JobInfo BuildJobInfo(IStorageConnection connection, string jobId, Job job, JobState state, DateTime? timestamp)
        {
            var customId = GetCustomId(connection, jobId);
            var isRecurring = job?.IsRecurring() == true;

            Type eventType = null;
            Type handlerType = null;

            if (isRecurring)
                handlerType = new HangfireRecurringJobInfo(job).HandlerType;
            else if (job != null && job.Method.IsGenericMethod)
                eventType = job.Method.GetGenericArguments()[0];

            return new JobInfo(jobId, customId, eventType?.Name, eventType, handlerType, isRecurring, state, ToUtcOffset(timestamp));
        }

        /// <summary>Storages may return timestamps with <see cref="DateTimeKind.Unspecified"/>; Hangfire stores them in UTC.</summary>
        private static DateTimeOffset? ToUtcOffset(DateTime? timestamp)
        {
            if (!timestamp.HasValue)
                return null;

            var value = timestamp.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc)
                : timestamp.Value.ToUniversalTime();

            return new DateTimeOffset(value);
        }

        /// <summary>
        /// Returns the number of background jobs in the specified state.
        /// </summary>
        /// <param name="state">The job state to count.</param>
        /// <returns>The total number of jobs in the given state.</returns>
        public long CountJobs(JobState state)
        {
            var stats = _jobStorage.GetMonitoringApi().GetStatistics();

            switch (state)
            {
                case JobState.Enqueued: return stats.Enqueued;
                case JobState.Processing: return stats.Processing;
                case JobState.Succeeded: return stats.Succeeded;
                case JobState.Failed: return stats.Failed;
                case JobState.Cancelled: return stats.Deleted;
                case JobState.Scheduled: return stats.Scheduled;
                default: return 0;
            }
        }

        /// <summary>
        /// Returns a summary with the job count for every <see cref="JobState"/>.
        /// </summary>
        /// <returns>A <see cref="JobStateSummary"/> containing counts for all states.</returns>
        public JobStateSummary GetStateSummary()
        {
            var stats = _jobStorage.GetMonitoringApi().GetStatistics();

            return new JobStateSummary(
                enqueued: stats.Enqueued,
                processing: stats.Processing,
                succeeded: stats.Succeeded,
                failed: stats.Failed,
                cancelled: stats.Deleted,
                scheduled: stats.Scheduled);
        }

        private static string GetCustomId(IStorageConnection connection, string jobId)
        {
            try
            {
                return JobParameters.ReadCustomId(connection, jobId);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ExecutionFlow: Failed to get custom ID for job '{0}': {1}", jobId, ex.Message);
                return null;
            }
        }
    }
}
