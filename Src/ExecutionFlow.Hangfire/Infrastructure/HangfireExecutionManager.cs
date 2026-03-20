using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
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

            return InfraUtils
                .ReadAll(monitoringApi.ProcessingJobs)
                .Any(x => GetCustomId(x.Key) == customId);
        }

        public bool IsPending(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();

            return queues
                .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                .Any(x => GetCustomId(x.Key) == customId);
        }

        public void Cancel(string customId)
        {
            var jobId = FindJobId(customId);
            if (jobId != null)
                _jobClient.Delete(jobId);
        }

        private string FindJobId(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            var processingID = InfraUtils
                .ReadAll(monitoringApi.ProcessingJobs)
                .FirstOrDefault(x => GetCustomId(x.Key) == customId)
                .Key;

            if (!string.IsNullOrEmpty(processingID))
                return processingID;

            var queues = monitoringApi.Queues();
            return queues
                .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                .FirstOrDefault(x => GetCustomId(x.Key) == customId)
                .Key;
        }

        public IEnumerable<JobInfo> GetJobs(JobState state)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            switch (state)
            {
                case JobState.Enqueued:
                    return monitoringApi
                        .Queues()
                        .SelectMany(q => InfraUtils.ReadAll(q.Name, monitoringApi.EnqueuedJobs))
                        .Select(job => BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.EnqueuedAt));
                case JobState.Processing:
                    return InfraUtils
                        .ReadAll(monitoringApi.ProcessingJobs)
                        .Select(job => BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.StartedAt));
                case JobState.Succeeded:
                    return InfraUtils
                        .ReadAll(monitoringApi.SucceededJobs)
                        .Select(job => BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.SucceededAt));
                case JobState.Failed:
                    return InfraUtils
                        .ReadAll(monitoringApi.FailedJobs)
                        .Select(job => BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.FailedAt));
                case JobState.Cancelled:
                    return InfraUtils
                        .ReadAll(monitoringApi.DeletedJobs)
                        .Select(job => BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.DeletedAt));
                default:
                    return Array.Empty<JobInfo>();
            }
        }

        private JobInfo BuildJobInfo(string jobId, Job job, InvocationData invocationData, JobState state, DateTime? timestamp)
        {
            var customId = GetCustomId(jobId);

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

        private string GetCustomId(string jobId)
        {
            using (var connection = _jobStorage.GetConnection())
                return connection.GetJobParameter(jobId, HangfireDispatcher.EventId);
        }
    }
}
