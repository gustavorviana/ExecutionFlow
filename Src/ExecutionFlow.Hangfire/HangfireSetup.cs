using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // Hangfire's filters and activator are process-wide, so only one full setup can be active per process:
        // the last Build() wins and removes the previous setup's global registrations (Hangfire only; other processors
        // aren't limited by this).
        private static readonly object GlobalStateLock = new object();
        private static HangfireSetup _activeFullSetup;

        private FlowEngineJobActivator _activator;
        private HangfireStateFilter _stateFilter;
        private DeduplicationCleanupFilter _cleanupFilter;
        private HandlerJobFilterProvider _filterProvider;
        private PrerequisiteGateFilter _gateFilter;

        /// <summary>
        /// Gets the execution manager bound to this setup's storage, available after <see cref="Build"/> or
        /// <see cref="BuildDispatcherOnly(IBackgroundJobClient, JobStorage, IServiceProvider)"/>.
        /// </summary>
        public IExecutionManager ExecutionManager { get; private set; }

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

            foreach (var handlerType in options.RecurringTimeZones.Keys)
            {
                if (!RecurringHandlers.ContainsKey(handlerType))
                    throw new InvalidOperationException(
                        $"SetJobTimeZone references type '{handlerType.FullName}' which is not registered as a recurring handler.");
            }

            ValidateTimeZone(options.RecurringTimeZone, "RecurringTimeZone");

            foreach (var registration in RecurringHandlers.Values)
            {
                // A dependent triggered by its prerequisites needs no schedule and ignores one it has (RunOnOwnSchedule keeps it).
                if (IsTriggeredByPlan(registration.HandlerType))
                {
                    if (!string.IsNullOrWhiteSpace(registration.Cron))
                        Trace.TraceWarning(
                            "ExecutionFlow: '{0}' depends on other handlers in the execution plan, so its prerequisites trigger it and its cron '{1}' is ignored.",
                            registration.HandlerType.FullName, registration.Cron);
                }
                else if (string.IsNullOrWhiteSpace(registration.Cron))
                    throw new InvalidOperationException(
                        $"Recurring handler '{registration.HandlerType.FullName}' has no schedule. Add [Recurring(\"<cron>\")] to the class.");

                ValidateTimeZone(RecurringJobResolver.ResolveTimeZoneId(registration, options), registration.HandlerType.FullName);
            }
        }

        private static void ValidateTimeZone(string timeZoneId, string source)
        {
            if (timeZoneId == null)
                return;

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException)
            {
                throw new InvalidOperationException(
                    $"Time zone '{timeZoneId}' configured for {source} was not found on this host. " +
                    "On .NET Framework only Windows time zone IDs are available.", ex);
            }
        }

        /// <summary>
        /// Sets the current <see cref="JobActivator"/> to a <see cref="FlowEngineJobActivator"/> backed by this setup,
        /// enabling Hangfire to resolve ExecutionFlow handlers.
        /// </summary>
        /// <returns>This instance for chaining.</returns>
        public HangfireSetup ConfigureActivator()
        {
            JobActivator.Current = GetOrCreateActivator();
            return this;
        }

        /// <summary>This setup's own activator: the one <see cref="ConfigureActivator"/> installed, or a new one.</summary>
        private FlowEngineJobActivator GetOrCreateActivator()
        {
            return _activator ?? (_activator = new FlowEngineJobActivator(this));
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

                // Never reuse another setup's activator (JobActivator.Current may belong to a different setup).
                if (serviceProvider == null)
                    serviceProvider = GetOrCreateActivator();

                if (jobStorage == null)
                    jobStorage = JobStorage.Current;

                if (jobClient == null)
                    jobClient = new BackgroundJobClient(jobStorage);

                if (serviceProvider is IFlowServiceRegistry serviceRegistry)
                    RegisterServices(serviceRegistry, jobClient, jobStorage);

                InitGenerators(serviceProvider);

                RegisterGlobalFilters(serviceProvider);
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

            // Available in both modes: the manager only needs the storage and client, not the handlers.
            ExecutionManager = new HangfireExecutionManager(jobClient, jobStorage);

            if (serviceProvider is IFlowServiceRegistry serviceRegistry)
                RegisterDispatcher(serviceRegistry, dispatcher);

            _built = true;
            return dispatcher;
        }

        /// <summary>
        /// Registers this setup's filters in Hangfire's process-wide collections, replacing those of the previously
        /// active full setup (the last full build wins).
        /// </summary>
        private void RegisterGlobalFilters(IServiceProvider serviceProvider)
        {
            lock (GlobalStateLock)
            {
                _activeFullSetup?.RemoveGlobalFilters();

                _stateFilter = new HangfireStateFilter(this, serviceProvider, StateHandlerTypes, Options.HookErrorHandler);
                _cleanupFilter = new DeduplicationCleanupFilter();
                _filterProvider = new HandlerJobFilterProvider(this, Options);

                GlobalJobFilters.Filters.Add(_stateFilter, HangfireStateFilter.FilterOrder);

                if (Options.Plan != null)
                {
                    _gateFilter = new PrerequisiteGateFilter(this);
                    GlobalJobFilters.Filters.Add(_gateFilter, PrerequisiteGateFilter.FilterOrder);
                }

                GlobalJobFilters.Filters.Add(_cleanupFilter);
                JobFilterProviders.Providers.Add(_filterProvider);

                _activeFullSetup = this;
            }
        }

        private void RemoveGlobalFilters()
        {
            if (_stateFilter != null)
                GlobalJobFilters.Filters.Remove(_stateFilter);
            if (_cleanupFilter != null)
                GlobalJobFilters.Filters.Remove(_cleanupFilter);
            if (_filterProvider != null)
                JobFilterProviders.Providers.Remove(_filterProvider);
            if (_gateFilter != null)
                GlobalJobFilters.Filters.Remove(_gateFilter);
        }

        /// <summary>
        /// Whether the handler is a dependent in the execution plan that its prerequisites trigger: it has prerequisites and
        /// doesn't run on its own schedule.
        /// </summary>
        internal bool IsTriggeredByPlan(Type handlerType)
        {
            return Options.Plan != null && Options.Plan.TryGet(handlerType, out var step)
                && step.Prerequisites.Count > 0 && !step.RunsOnOwnSchedule;
        }

        /// <summary>Triggers a recurring job now. Replaceable in tests, where there's no real storage behind the job manager.</summary>
        internal Action<JobStorage, string> TriggerRecurringJob { get; set; } =
            (storage, jobId) => new RecurringJobManager(storage).TriggerJob(jobId);

        /// <summary>
        /// Schedules a trigger of a dependent after a delay (its minimum interval). Replaceable in tests.
        /// </summary>
        internal Action<JobStorage, string, TimeSpan> ScheduleDependentTrigger { get; set; } =
            (storage, dependentId, delay) => new BackgroundJobClient(storage)
                .Schedule<HangfireJobDispatcher>(d => d.TriggerPlanDependent(null, dependentId), delay);

        /// <summary>The current UTC time, for minimum intervals. Replaceable in tests.</summary>
        internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        /// <summary>The recurring job ID of a registered handler, or its full name when it isn't registered.</summary>
        internal string ResolveRecurringJobId(Type handlerType)
        {
            return RecurringHandlers.TryGetValue(handlerType, out var registration) && JobIdGenerator != null
                ? RecurringJobResolver.ResolveId(registration, JobIdGenerator)
                : handlerType.FullName;
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
                .AddSingleton<IJobIdGenerator>(Options.JobIdGeneratorType);
        }

        private void RegisterDispatcher(IFlowServiceRegistry serviceRegistry, HangfireDispatcher dispatcher)
        {
            serviceRegistry
                .AddSingleton(dispatcher)
                .AddSingleton<IRecurringTrigger>(dispatcher)
                .AddSingleton<IEventDispatcher>(dispatcher)
                .AddSingleton(ExecutionManager);
        }

        private void RegisterRecurring(JobStorage jobStorage)
        {
            var recurringJobManager = new RecurringJobManager(jobStorage);
            var registeredIds = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var registration in RecurringHandlers.Values)
            {
                var jobId = RecurringJobResolver.ResolveId(registration, JobIdGenerator);
                if (registeredIds.TryGetValue(jobId, out var otherHandler))
                    throw new InvalidOperationException(
                        $"Recurring handlers '{otherHandler.FullName}' and '{registration.HandlerType.FullName}' resolve to the same job ID '{jobId}'.");
                registeredIds.Add(jobId, registration.HandlerType);

                // A handler that doesn't auto-run never fires on its own; it still runs through Trigger.
                // A dependent triggered by its prerequisites never fires on schedule.
                var cron = RecurringJobResolver.IsAutoRun(registration.HandlerType, Options) && !IsTriggeredByPlan(registration.HandlerType)
                    ? registration.Cron
                    : Cron.Never();

                recurringJobManager.AddOrUpdate<HangfireJobDispatcher>(
                    jobId,
                    dispatcher => dispatcher.DispatchRecurringAsync(null, registration.HandlerType, CancellationToken.None),
                    cron,
                    new RecurringJobOptions { TimeZone = RecurringJobResolver.ResolveTimeZone(registration, Options) });
            }

            if (!Options.RemoveOrphanRecurringJobs)
                return;

            using (var connection = jobStorage.GetConnection())
            {
                foreach (var job in connection.GetRecurringJobs())
                    if (!registeredIds.ContainsKey(job.Id) && job.Job.IsRecurring())
                        recurringJobManager.RemoveIfExists(job.Id);
            }
        }
    }
}