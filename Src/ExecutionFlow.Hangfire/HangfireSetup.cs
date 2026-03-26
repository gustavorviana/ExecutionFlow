using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExecutionFlow.Hangfire
{
    public class HangfireSetup : ExecutionFlowSetup<HangfireOptions>
    {
        private IHangfireDispatcher _dispatcher;
        private readonly object _buildLock = new object();
        public IReadOnlyList<Type> StateHandlerTypes => Options?.StateHandlerTypes;
        public IJobIdGenerator JobIdGenerator { get; internal set; }
        public IHangfireJobName JobNameGenerator { get; internal set; }

        protected override void OnConfigured(HangfireOptions options)
        {
            foreach (var kvp in options.RecurringAutoRun)
            {
                var handlerType = kvp.Key;
                if (!RecurringHandlers.ContainsKey(handlerType))
                    throw new InvalidOperationException(
                        $"SetJobAutoRun references type '{handlerType.FullName}' which is not registered as a recurring handler.");
            }
        }

        public HangfireSetup ConfigureActivator()
        {
            JobActivator.Current = new FlowEngineJobActivator(this);
            return this;
        }

        public IHangfireDispatcher Build(IBackgroundJobClient jobClient = null, JobStorage jobStorage = null, IServiceProvider serviceProvider = null)
        {
            if (_dispatcher != null)
                return _dispatcher;

            lock (_buildLock)
            {
                if (_dispatcher != null)
                    return _dispatcher;

                if (serviceProvider == null)
                    serviceProvider = (JobActivator.Current as FlowEngineJobActivator) ?? new FlowEngineJobActivator(this);

                if (jobStorage == null)
                    jobStorage = JobStorage.Current;

                if (jobClient == null)
                    jobClient = new BackgroundJobClient(jobStorage);

                if (serviceProvider is IFlowServiceRegistry serviceRegistry)
                    RegisterServices(serviceRegistry, jobClient, jobStorage);

                JobIdGenerator = (IJobIdGenerator)serviceProvider.GetService(typeof(IJobIdGenerator));
                JobNameGenerator = (IHangfireJobName)serviceProvider.GetService(typeof(IHangfireJobName));

                GlobalJobFilters.Filters.Add(new HangfireStateFilter(this, serviceProvider, StateHandlerTypes));
                GlobalJobFilters.Filters.Add(new HangfireAutoRunFilter(this, Options));
                JobFilterProviders.Providers.Add(new HandlerJobFilterProvider(this));
                RegisterRecurring(jobStorage);
                return _dispatcher = new HangfireDispatcher(jobClient, jobStorage, JobIdGenerator, this);
            }
        }

        private void RegisterServices(IFlowServiceRegistry serviceRegistry, IBackgroundJobClient jobClient, JobStorage jobStorage)
        {
            foreach (var kvp in Options.OptionValues)
                serviceRegistry.AddSingleton(kvp.Key, kvp.Value);

            serviceRegistry.AddSingleton(() => jobClient)
                .AddSingleton(() => jobStorage)
                .AddSingleton(() => _dispatcher)
                .RegisterLoggerFactory(Options.LoggerFactoryTypes)
                .AddSingleton<IHangfireJobName>(Options.JobNameType)
                .AddSingleton<IJobIdGenerator>(Options.JobIdGeneratorType)
                .AddSingleton<IExecutionManager>(typeof(HangfireExecutionManager))
                .AddSingleton<IRecurringTrigger>(() => _dispatcher)
                .AddSingleton<IEventDispatcher>(() => _dispatcher);
        }

        private void RegisterRecurring(JobStorage jobStorage)
        {
            var recurringJobManager = new RecurringJobManager(jobStorage);
            var registeredIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var registration in RecurringHandlers.Values)
            {
                var jobId = JobIdGenerator.GenerateId(registration.HandlerType);
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

                var handlerType = HangfireJobInfo.Create(job)?.GetHandlerType(_registry);
                if (handlerType == null)
                    return Array.Empty<JobFilter>();

                return handlerType
                    .GetCustomAttributes(true)
                    .OfType<JobFilterAttribute>()
                    .Select((attr, i) => new JobFilter(attr, JobFilterScope.Type, i));
            }
        }
    }
}