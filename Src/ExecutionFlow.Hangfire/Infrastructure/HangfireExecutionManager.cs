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
    public class HangfireExecutionManager : IExecutionManager
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly JobStorage _jobStorage;

        public HangfireExecutionManager(IBackgroundJobClient jobClient, JobStorage jobStorage)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        }

        public bool IsRunning(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            using (var connection = _jobStorage.GetConnection())
                return InfraUtils
                    .ReadAll(monitoringApi.ProcessingJobs)
                    .Any(x => GetCustomId(connection, x.Key) == customId);
        }

        public bool IsPending(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();

            using (var connection = _jobStorage.GetConnection())
                return queues
                    .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                    .Any(x => GetCustomId(connection, x.Key) == customId);
        }

        public void Cancel(string customId)
        {
            var jobId = FindJobId(customId);
            if (jobId != null)
                _jobClient.Delete(jobId);
        }

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

            return new JobInfo(jobId, customId, eventTypeName, eventType, state, stateChangedAt);
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
