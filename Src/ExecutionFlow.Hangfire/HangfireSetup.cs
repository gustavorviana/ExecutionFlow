using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using ExecutionFlow.Hangfire.Filters;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExecutionFlow.Hangfire
{
    public class HangfireSetup : ExecutionFlowSetup<HangfireOptions>, IHangfireJobName
    {
        private IDispatcher _dispatcher;
        public IReadOnlyList<Type> StateHandlerTypes => Options?.StateHandlerTypes;

        protected override void OnConfigured(HangfireOptions options)
        {
            foreach (var kvp in options.JobAutoRunSettings)
            {
                var handlerType = kvp.Key;
                var isRegistered = RecurringHandlers.Any(r => r.HandlerType == handlerType);
                if (!isRegistered)
                    throw new InvalidOperationException(
                        $"SetJobAutoRun references type '{handlerType.FullName}' which is not registered as a recurring handler.");
            }
        }

        public HangfireSetup ConfigureActivator()
        {
            JobActivator.Current = new FlowEngineJobActivator(this);
            return this;
        }

        public IDispatcher Build(IBackgroundJobClient jobClient = null, JobStorage jobStorage = null, JobActivator jobActivator = null)
        {
            if (_dispatcher != null)
                return _dispatcher;

            if (jobActivator == null)
                jobActivator = JobActivator.Current;

            if (jobStorage == null)
                jobStorage = JobStorage.Current;

            if (jobClient == null)
                jobClient = new BackgroundJobClient(jobStorage);

            GlobalJobFilters.Filters.Add(new HangfireStateFilter(this, jobActivator, StateHandlerTypes));
            GlobalJobFilters.Filters.Add(new HangfireAutoRunFilter(this, Options.AutoRunRecurring, Options.JobAutoRunSettings));
            JobFilterProviders.Providers.Add(new HandlerJobFilterProvider(this));
            RegisterRecurring(jobStorage);
            return _dispatcher = new HangfireDispatcher(jobClient, jobStorage);
        }

        private void RegisterRecurring(JobStorage jobStorage)
        {
            var recurringJobManager = new RecurringJobManager(jobStorage);
            var registeredIds = new HashSet<string>();

            foreach (var registration in RecurringHandlers.Where(r => r.IsRecurring))
            {
                var jobId = registration.HandlerType.FullName;
                registeredIds.Add(jobId);

                recurringJobManager.AddOrUpdate<HangfireJobDispatcher>(
                    jobId,
                    dispatcher => dispatcher.DispatchRecurringAsync(null, registration.HandlerType, CancellationToken.None),
                    registration.Cron);
            }

            if (!Options.RemoveOrphanRecurringJobs)
                return;

            using (var connection = jobStorage.GetConnection())
            {
                var existingJobs = connection.GetRecurringJobs();
                foreach (var job in existingJobs)
                    if (!registeredIds.Contains(job.Id))
                        recurringJobManager.RemoveIfExists(job.Id);
            }
        }

        public string GetName(Job job)
        {
            var handlerInfo = GetEventInfo(job);
            if (handlerInfo != null)
                return string.IsNullOrEmpty(handlerInfo.DisplayName) ? handlerInfo.HandlerType.FullName : handlerInfo.DisplayName;

            if (job.Args.Count == 3 && job.Args[1] is Type jobType)
                return jobType.FullName;

            return $"{job.Method.DeclaringType.FullName}.{job.Method.Name}";
        }

        private HandlerRegistration GetEventInfo(Job job)
        {
            if (job.TryGetEventHandler(this, out var eventType))
                return EventHandlers.TryGetValue(eventType, out var eventHandler) ? eventHandler : null;

            var handlerType = job.GetRecurringHandlerType(this);
            return handlerType == null ? null : RecurringHandlers.FirstOrDefault(x => x.HandlerType.FullName == handlerType.FullName);
        }

        private class HandlerJobFilterProvider : IJobFilterProvider
        {
            private readonly IExecutionFlowRegistry _registry;


            public HandlerJobFilterProvider(IExecutionFlowRegistry registry)
            {
                _registry = registry;
            }

            public IEnumerable<JobFilter> GetFilters(Job job)
            {
                if (job == null)
                    return Array.Empty<JobFilter>();

                var handlerType = job.GetHandlerType(_registry);
                if (handlerType == null)
                    return Enumerable.Empty<JobFilter>();

                return handlerType
                    .GetCustomAttributes(true)
                    .OfType<JobFilterAttribute>()
                    .Select((attr, i) => new JobFilter(attr, JobFilterScope.Type, i));
            }
        }
    }
}
