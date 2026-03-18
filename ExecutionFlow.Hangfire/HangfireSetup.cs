using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using ExecutionFlow.Hangfire.Filters;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
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

        public IDispatcher Build(IBackgroundJobClient jobClient = null, JobStorage jobStorage = null)
        {
            if (_dispatcher != null)
                return _dispatcher;

            if (jobStorage == null)
                jobStorage = JobStorage.Current;

            if (jobClient == null)
                jobClient = new BackgroundJobClient(jobStorage);

            GlobalJobFilters.Filters.Add(new HangfireStateFilter(StateHandlerTypes.Select(Activator.CreateInstance).ToArray()));
            GlobalJobFilters.Filters.Add(new HangfireAutoRunFilter(Options.AutoRunRecurring, Options.JobAutoRunSettings));
            RegisterRecurring(new RecurringJobManager(jobStorage));
            return _dispatcher = new HangfireDispatcher(jobClient, jobStorage);
        }

        private void RegisterRecurring(IRecurringJobManager recurringJobManager)
        {
            foreach (var registration in Registrations.Where(r => r.IsRecurring))
            {
                recurringJobManager.AddOrUpdate<HangfireJobDispatcher>(
                    registration.HandlerType.FullName,
                    dispatcher => dispatcher.DispatchRecurringAsync(null, registration.HandlerType, CancellationToken.None),
                    registration.Cron);
            }
        }
    }
}
