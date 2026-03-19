using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using ExecutionFlow.Hangfire.Filters;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExecutionFlow.Hangfire
{
    public class HangfireSetup : ExecutionFlowSetup<HangfireOptions>
    {
        private IDispatcher _dispatcher;
        public IReadOnlyList<Type> StateHandlerTypes => Options?.StateHandlerTypes;

        protected override void OnConfigured(HangfireOptions options)
        {
            foreach (var kvp in options.JobAutoRunSettings)
            {
                var handlerType = kvp.Key;
                var isRegistered = Registrations.Any(r => r.IsRecurring && r.HandlerType == handlerType);
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

            GlobalJobFilters.Filters.Add(new HangfireStateFilter(jobActivator, StateHandlerTypes));
            GlobalJobFilters.Filters.Add(new HangfireAutoRunFilter(Options.AutoRunRecurring, Options.JobAutoRunSettings));
            RegisterRecurring(jobStorage);
            return _dispatcher = new HangfireDispatcher(jobClient, jobStorage);
        }

        private void RegisterRecurring(JobStorage jobStorage)
        {
            var recurringJobManager = new RecurringJobManager(jobStorage);
            var registeredIds = new HashSet<string>();

            foreach (var registration in Registrations.Where(r => r.IsRecurring))
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
    }
}
