using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Storage.Monitoring;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireExecutionManager : IExecutionManager
    {
        private const int PageSize = 100;
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
            var from = 0;

            while (true)
            {
                var jobs = monitoringApi.ProcessingJobs(from, PageSize);
                if (jobs == null || jobs.Count == 0)
                    break;

                foreach (var job in jobs)
                {
                    var name = GetCustomId(job.Key);
                    if (name == customId)
                        return true;
                }

                if (jobs.Count < PageSize)
                    break;

                from += PageSize;
            }

            return false;
        }

        public bool IsPending(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();

            foreach (var queue in queues)
            {
                var from = 0;

                while (true)
                {
                    var jobs = monitoringApi.EnqueuedJobs(queue.Name, from, PageSize);
                    if (jobs == null || jobs.Count == 0)
                        break;

                    foreach (var job in jobs)
                    {
                        var name = GetCustomId(job.Key);
                        if (name == customId)
                            return true;
                    }

                    if (jobs.Count < PageSize)
                        break;

                    from += PageSize;
                }
            }

            return false;
        }

        public void Cancel(string customId)
        {
            var jobId = FindJobId(customId);
            if (jobId != null)
            {
                _jobClient.Delete(jobId);
            }
        }

        private string FindJobId(string customId)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();

            // Check processing jobs
            var from = 0;
            while (true)
            {
                var jobs = monitoringApi.ProcessingJobs(from, PageSize);
                if (jobs == null || jobs.Count == 0)
                    break;

                foreach (var job in jobs)
                {
                    var name = GetCustomId(job.Key);
                    if (name == customId)
                        return job.Key;
                }

                if (jobs.Count < PageSize)
                    break;

                from += PageSize;
            }

            // Check enqueued jobs
            var queues = monitoringApi.Queues();
            foreach (var queue in queues)
            {
                from = 0;
                while (true)
                {
                    var jobs = monitoringApi.EnqueuedJobs(queue.Name, from, PageSize);
                    if (jobs == null || jobs.Count == 0)
                        break;

                    foreach (var job in jobs)
                    {
                        var name = GetCustomId(job.Key);
                        if (name == customId)
                            return job.Key;
                    }

                    if (jobs.Count < PageSize)
                        break;

                    from += PageSize;
                }
            }

            return null;
        }

        public IEnumerable<JobInfo> GetJobs(JobState state)
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var results = new List<JobInfo>();

            switch (state)
            {
                case JobState.Enqueued:
                    foreach (var queue in monitoringApi.Queues())
                    {
                        var from = 0;
                        while (true)
                        {
                            var jobs = monitoringApi.EnqueuedJobs(queue.Name, from, PageSize);
                            if (jobs == null || jobs.Count == 0)
                                break;

                            foreach (var job in jobs)
                                results.Add(BuildJobInfo(job.Key, job.Value.Job, job.Value.InvocationData, state, job.Value.EnqueuedAt));

                            if (jobs.Count < PageSize)
                                break;

                            from += PageSize;
                        }
                    }
                    break;

                case JobState.Processing:
                    CollectJobs(monitoringApi.ProcessingJobs, results, state,
                        v => v.StartedAt, v => v.Job, v => v.InvocationData);
                    break;

                case JobState.Succeeded:
                    CollectJobs(monitoringApi.SucceededJobs, results, state,
                        v => v.SucceededAt, v => v.Job, v => v.InvocationData);
                    break;

                case JobState.Failed:
                    CollectJobs(monitoringApi.FailedJobs, results, state,
                        v => v.FailedAt, v => v.Job, v => v.InvocationData);
                    break;

                case JobState.Cancelled:
                    CollectJobs(monitoringApi.DeletedJobs, results, state,
                        v => v.DeletedAt, v => v.Job, v => v.InvocationData);
                    break;
            }

            return results;
        }

        private void CollectJobs<TDto>(
            Func<int, int, JobList<TDto>> fetch,
            List<JobInfo> results,
            JobState state,
            Func<TDto, DateTime?> getTimestamp,
            Func<TDto, global::Hangfire.Common.Job> getJob,
            Func<TDto, global::Hangfire.Storage.InvocationData> getInvocationData)
        {
            var from = 0;
            while (true)
            {
                var jobs = fetch(from, PageSize);
                if (jobs == null || jobs.Count == 0)
                    break;

                foreach (var job in jobs)
                    results.Add(BuildJobInfo(job.Key, getJob(job.Value), getInvocationData(job.Value), state, getTimestamp(job.Value)));

                if (jobs.Count < PageSize)
                    break;

                from += PageSize;
            }
        }

        private JobInfo BuildJobInfo(string jobId, global::Hangfire.Common.Job job, global::Hangfire.Storage.InvocationData invocationData, JobState state, DateTime? timestamp)
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
