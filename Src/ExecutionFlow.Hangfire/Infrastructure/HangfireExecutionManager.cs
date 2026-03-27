using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Manages Hangfire job execution state, providing methods to query, cancel, and retry
    /// both event jobs (by custom ID) and recurring jobs (by handler type).
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
        /// Determines whether an event job with the specified custom ID is currently being processed.
        /// </summary>
        /// <param name="customId">The custom identifier to search for.</param>
        /// <returns><c>true</c> if a job with the given custom ID is processing; otherwise, <c>false</c>.</returns>
        public bool IsRunning(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
                return InfraUtils
                    .ReadAll(monitoringApi.ProcessingJobs)
                    .Any(x => GetCustomId(connection, x.Key) == customId);
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
        /// Determines whether an event job with the specified custom ID is enqueued and waiting to be processed.
        /// </summary>
        /// <param name="customId">The custom identifier to search for.</param>
        /// <returns><c>true</c> if a job with the given custom ID is enqueued; otherwise, <c>false</c>.</returns>
        public bool IsPending(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();

            using (var connection = _jobStorage.GetConnection())
                return queues
                    .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                    .Any(x => GetCustomId(connection, x.Key) == customId);
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
        /// Cancels (deletes) a running or pending event job that matches the specified custom ID.
        /// </summary>
        /// <param name="customId">The custom identifier of the job to cancel.</param>
        public void Cancel(string customId)
        {
            var jobId = FindJobId(customId);
            if (jobId != null)
                _jobClient.Delete(jobId);
        }

        /// <summary>
        /// Cancels (deletes) a running or pending recurring job that matches the specified handler type.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the job to cancel.</param>
        public void Cancel(Type handlerType)
        {
            var jobId = FindRecurringJobId(handlerType);
            if (jobId != null)
                _jobClient.Delete(jobId);
        }

        /// <summary>
        /// Retries a failed event job that matches the specified custom ID by re-enqueuing it.
        /// </summary>
        /// <param name="customId">The custom identifier of the failed job to retry.</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        public bool Retry(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                var failedJobId = InfraUtils
                    .ReadAll(monitoringApi.FailedJobs)
                    .FirstOrDefault(x => GetCustomId(connection, x.Key) == customId)
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

        private string FindJobId(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                var processingID = InfraUtils
                    .ReadAll(monitoringApi.ProcessingJobs)
                    .FirstOrDefault(x => GetCustomId(connection, x.Key) == customId)
                    .Key;

                if (!string.IsNullOrEmpty(processingID))
                    return processingID;

                var queues = monitoringApi.Queues();
                return queues
                    .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                    .FirstOrDefault(x => GetCustomId(connection, x.Key) == customId)
                    .Key;
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

            var queues = monitoringApi.Queues();
            return queues
                .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                .FirstOrDefault(x => x.Value.Job.IsRecurringOfType(handlerType))
                .Key;
        }

        /// <summary>
        /// Retrieves all background jobs in the specified state, including both event and recurring jobs.
        /// </summary>
        /// <param name="state">The job state to filter by.</param>
        /// <returns>A collection of <see cref="JobInfo"/> representing the matching jobs.</returns>
        public IEnumerable<JobInfo> GetJobs(JobState state)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
            {
                switch (state)
                {
                    case JobState.Enqueued:
                        return monitoringApi
                            .Queues()
                            .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                            .Select(job => BuildJobInfo(connection, job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.EnqueuedAt))
                            .ToList();
                    case JobState.Processing:
                        return InfraUtils
                            .ReadAll(monitoringApi.ProcessingJobs)
                            .Select(job => BuildJobInfo(connection, job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.StartedAt))
                            .ToList();
                    case JobState.Succeeded:
                        return InfraUtils
                            .ReadAll(monitoringApi.SucceededJobs)
                            .Select(job => BuildJobInfo(connection, job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.SucceededAt))
                            .ToList();
                    case JobState.Failed:
                        return InfraUtils
                            .ReadAll(monitoringApi.FailedJobs)
                            .Select(job => BuildJobInfo(connection, job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.FailedAt))
                            .ToList();
                    case JobState.Cancelled:
                        return InfraUtils
                            .ReadAll(monitoringApi.DeletedJobs)
                            .Select(job => BuildJobInfo(connection, job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.DeletedAt))
                            .ToList();
                    default:
                        return Array.Empty<JobInfo>();
                }
            }
        }

        private static JobInfo BuildJobInfo(IStorageConnection connection, string jobId, Job job, InvocationData invocationData, JobState state, DateTime? timestamp)
        {
            var customId = GetCustomId(connection, jobId);
            var isRecurring = job?.IsRecurring() == true;

            string eventTypeName = null;
            Type eventType = null;

            if (job != null && job.Method.IsGenericMethod)
            {
                var genericArgs = job.Method.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    eventType = genericArgs[0];
                    eventTypeName = eventType.Name;
                }
            }

            if (eventTypeName == null && invocationData != null)
            {
                var typeString = invocationData.Method;
                if (!string.IsNullOrEmpty(typeString))
                {
                    eventTypeName = typeString;
                }
            }

            DateTimeOffset? stateChangedAt = timestamp.HasValue
                ? new DateTimeOffset(timestamp.Value)
                : (DateTimeOffset?)null;

            return new JobInfo(jobId, customId, eventTypeName, eventType, isRecurring, state, stateChangedAt);
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
                cancelled: stats.Deleted);
        }

        private static string GetCustomId(IStorageConnection connection, string jobId)
        {
            try
            {
                return connection.GetJobParameter(jobId, ContextConsts.CustomId);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ExecutionFlow: Failed to get custom ID for job '{0}': {1}", jobId, ex.Message);
                return null;
            }
        }
    }
}
