using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using System;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    internal class HangfireNoConcurrentFilter : IClientFilter
    {
        private readonly IExecutionFlowRegistry _registry;
        private readonly HangfireOptions _options;
        private readonly JobStorage _jobStorage;

        public HangfireNoConcurrentFilter(IExecutionFlowRegistry registry, HangfireOptions options, JobStorage jobStorage)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _options = options;
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        }

        public void OnCreating(CreatingContext context)
        {
            if (!context.Job.IsRecurring())
                return;

            var handlerType = HangfireJobInfo.Create(context.Job)?.GetHandlerType(_registry);
            if (handlerType == null || !ShouldDisableConcurrent(handlerType))
                return;

            if (HasActiveJob(handlerType))
                context.Canceled = true;
        }

        public void OnCreated(CreatedContext context) { }

        private bool HasActiveJob(Type handlerType)
        {
            var monitoring = _jobStorage.GetMonitoringApi();

            if (InfraUtils.ReadAll(monitoring.ProcessingJobs).Any(x => IsRecurringType(x.Job, handlerType)))
                return true;

            foreach (var queue in monitoring.Queues())
                if (InfraUtils.ReadAll(queue.Name, monitoring.EnqueuedJobs).Any(x => IsRecurringType(x.Job, handlerType)))
                    return true;

            return false;
        }

        private static bool IsRecurringType(Job job, Type handlerType)
        {
            return job?.IsRecurring() == true && HangfireRecurringJobInfo.GetJobType(job) == handlerType;
        }

        private bool ShouldDisableConcurrent(Type handlerType)
        {
            if (_options.RecurringDisableConcurrent.TryGetValue(handlerType, out var disable))
                return disable;

            return _options.GlobalDisableConcurrentExecution;
        }
    }
}
