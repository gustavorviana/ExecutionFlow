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
    /// <summary>
    /// Orchestrates the setup and initialization of ExecutionFlow with Hangfire, including handler registration,
    /// filter configuration, and dispatcher creation.
    /// </summary>
    public class HangfireSetup : ExecutionFlowSetup<HangfireOptions>
    {
        private bool _built;
        private readonly object _buildLock = new object();

        /// <summary>Gets the registered state handler types from the options.</summary>
        public IReadOnlyList<Type> StateHandlerTypes => Options?.StateHandlerTypes;

        /// <summary>Gets the job ID generator used to create recurring job identifiers.</summary>
        public IJobIdGenerator JobIdGenerator { get; internal set; }

        /// <summary>Gets the job name generator used to produce display names for Hangfire jobs.</summary>
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

        /// <summary>
        /// Sets the current <see cref="JobActivator"/> to a <see cref="FlowEngineJobActivator"/> backed by this setup,
        /// enabling Hangfire to resolve ExecutionFlow handlers.
        /// </summary>
        /// <returns>This instance for chaining.</returns>
        public HangfireSetup ConfigureActivator()
        {
            JobActivator.Current = new FlowEngineJobActivator(this);
            return this;
        }

        /// <summary>
        /// Builds and initializes the full ExecutionFlow pipeline, registering global filters, recurring jobs,
        /// and returning a dispatcher capable of publishing, scheduling, and triggering jobs.
        /// </summary>
        /// <param name="jobClient">The Hangfire background job client. If <c>null</c>, a default client is created.</param>
        /// <param name="jobStorage">The Hangfire job storage. If <c>null</c>, <see cref="JobStorage.Current"/> is used.</param>
        /// <param name="serviceProvider">The service provider for resolving dependencies. If <c>null</c>, the current job activator is used.</param>
        /// <returns>A fully configured <see cref="IHangfireDispatcher"/>.</returns>
        public IHangfireDispatcher Build(IBackgroundJobClient jobClient = null, JobStorage jobStorage = null, IServiceProvider serviceProvider = null)
        {
            lock (_buildLock)
            {
                ThrowIfBuilt();

                if (serviceProvider == null)
                    serviceProvider = (JobActivator.Current as FlowEngineJobActivator) ?? new FlowEngineJobActivator(this);

                if (jobStorage == null)
                    jobStorage = JobStorage.Current;

                if (jobClient == null)
                    jobClient = new BackgroundJobClient(jobStorage);

                if (serviceProvider is IFlowServiceRegistry serviceRegistry)
                    RegisterServices(serviceRegistry, jobClient, jobStorage);

                InitGenerators(serviceProvider);

                GlobalJobFilters.Filters.Add(new HangfireStateFilter(this, serviceProvider, StateHandlerTypes));
                GlobalJobFilters.Filters.Add(new HangfireAutoRunFilter(this, Options));
                JobFilterProviders.Providers.Add(new HandlerJobFilterProvider(this, Options));
                RegisterRecurring(jobStorage);

                return CreateDispatcher(jobClient, jobStorage, serviceProvider);
            }
        }

        /// <summary>
        /// Builds a lightweight dispatcher that can only publish and schedule jobs, without registering
        /// global filters or recurring jobs. Useful for producer-only scenarios.
        /// </summary>
        /// <param name="jobStorage">The Hangfire job storage to use.</param>
        /// <param name="serviceProvider">Optional service provider for resolving dependencies.</param>
        /// <returns>An <see cref="IEventDispatcher"/> for publishing jobs.</returns>
        public IEventDispatcher BuildDispatcherOnly(JobStorage jobStorage, IServiceProvider serviceProvider = null)
        {
            return BuildDispatcherOnly(new BackgroundJobClient(jobStorage), jobStorage, serviceProvider);
        }

        /// <summary>
        /// Builds a lightweight dispatcher that can only publish and schedule jobs, without registering
        /// global filters or recurring jobs. Useful for producer-only scenarios.
        /// </summary>
        /// <param name="jobClient">The Hangfire background job client.</param>
        /// <param name="jobStorage">The Hangfire job storage to use.</param>
        /// <param name="serviceProvider">Optional service provider for resolving dependencies.</param>
        /// <returns>An <see cref="IEventDispatcher"/> for publishing jobs.</returns>
        public IEventDispatcher BuildDispatcherOnly(IBackgroundJobClient jobClient, JobStorage jobStorage, IServiceProvider serviceProvider = null)
        {
            lock (_buildLock)
            {
                ThrowIfBuilt();

                if (jobClient == null) throw new ArgumentNullException(nameof(jobClient));
                if (jobStorage == null) throw new ArgumentNullException(nameof(jobStorage));

                if (serviceProvider == null)
                    serviceProvider = new FlowEngineJobActivator(this)
                        .AddSingleton<IJobIdGenerator>(Options.JobIdGeneratorType)
                        .AddSingleton<IHangfireJobName>(Options.JobNameType);

                InitGenerators(serviceProvider);
                return CreateDispatcher(jobClient, jobStorage, serviceProvider);
            }
        }

        private HangfireDispatcher CreateDispatcher(IBackgroundJobClient jobClient, JobStorage jobStorage, IServiceProvider serviceProvider)
        {
            var dispatcher = new HangfireDispatcher(jobClient, jobStorage, JobIdGenerator, this, Options);

            if (serviceProvider is IFlowServiceRegistry serviceRegistry)
                RegisterDispatcher(serviceRegistry, dispatcher);

            _built = true;
            return dispatcher;
        }

        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("HangfireSetup has already been built. Create a new instance to build again.");
        }

        private void InitGenerators(IServiceProvider serviceProvider)
        {
            JobIdGenerator = (IJobIdGenerator)serviceProvider.GetService(typeof(IJobIdGenerator));
            JobNameGenerator = (IHangfireJobName)serviceProvider.GetService(typeof(IHangfireJobName));
        }

        private void RegisterServices(IFlowServiceRegistry serviceRegistry, IBackgroundJobClient jobClient, JobStorage jobStorage)
        {
            foreach (var kvp in Options.OptionValues)
                serviceRegistry.AddSingleton(kvp.Key, kvp.Value);

            serviceRegistry.AddSingleton(() => jobClient)
                .AddSingleton(() => jobStorage)
                .RegisterLoggerFactory(Options.LoggerFactoryTypes)
                .AddSingleton<IHangfireJobName>(Options.JobNameType)
                .AddSingleton<IJobIdGenerator>(Options.JobIdGeneratorType)
                .AddSingleton<IExecutionManager>(typeof(HangfireExecutionManager));
        }

        private void RegisterDispatcher(IFlowServiceRegistry serviceRegistry, HangfireDispatcher dispatcher)
        {
            serviceRegistry
                .AddSingleton(dispatcher)
                .AddSingleton<IRecurringTrigger>(dispatcher)
                .AddSingleton<IEventDispatcher>(dispatcher);
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
    }
}