using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hangfire.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering ExecutionFlow with Hangfire in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ExecutionFlow in full mode (a consumer that runs handlers): handlers, lifecycle hooks, the dispatcher,
        /// the execution manager and the Hangfire integration. It also publishes, so don't combine it with
        /// <see cref="AddExecutionFlowDispatcher(IServiceCollection, Action{HangfireOptions})"/>.
        /// </summary>
        /// <remarks>
        /// The Hangfire integration is built when the dispatcher is first resolved. Apps running a generic host get that at
        /// startup automatically; other apps should call <see cref="StartExecutionFlow"/> after building the provider.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">An optional callback to configure <see cref="HangfireOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="AddExecutionFlowDispatcher(IServiceCollection, Action{HangfireOptions})"/> was already called.</exception>
        public static IServiceCollection AddHangfireToExecutionFlow(
            this IServiceCollection services,
            Action<HangfireOptions> configure = null)
        {
            if (services.Any(d => d.ServiceType == typeof(ProducerOnlyModeMarker)))
                throw new InvalidOperationException(
                    "ExecutionFlow is already registered in producer-only mode (AddExecutionFlowDispatcher). " +
                    "Use only AddHangfireToExecutionFlow: it runs handlers and also publishes. " +
                    "AddExecutionFlowDispatcher is for apps that don't run handlers.");

            services.AddSingleton<FullModeMarker>();

            var setup = new HangfireSetup();
            if (configure != null)
                setup.Configure(configure);

            foreach (var registration in setup.RecurringHandlers.Values)
                services.AddTransient(registration.HandlerType);

            foreach (var registration in setup.EventHandlers.Values)
                services.AddTransient(registration.HandlerType);

            foreach (var stateHandlerType in setup.StateHandlerTypes)
                services.AddTransient(stateHandlerType);

            foreach (var kvp in setup.Options.OptionValues)
                services.AddSingleton(kvp.Key, kvp.Value);

            foreach (var loggerFactoryType in setup.LoggerFactoryTypes)
                services.AddSingleton(typeof(IExecutionLoggerFactory), loggerFactoryType);

            services.AddSingleton<ExecutionLoggerFactory>();

            services.AddSingleton(typeof(IJobIdGenerator), setup.Options.JobIdGeneratorType);
            services.AddSingleton<IExecutionFlowRegistry>(setup);

            // Bind the job name generator and the internal job dispatcher to this setup's registry.
            services.AddSingleton(typeof(IHangfireJobName), sp =>
                ActivatorUtilities.CreateInstance(sp, setup.Options.JobNameType, setup));
            services.AddTransient(sp => new HangfireJobDispatcher(sp, setup));

            services.AddSingleton(sp =>
            {
                var jobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var jobStorage = sp.GetRequiredService<JobStorage>();

                return setup.Build(jobClient, jobStorage, sp);
            });

            services.AddSingleton(sp =>
            {
                sp.GetRequiredService<IHangfireDispatcher>();
                return setup.ExecutionManager;
            });

            services.AddSingleton<IRecurringTrigger>(sp => sp.GetRequiredService<IHangfireDispatcher>());
            services.AddSingleton<IEventDispatcher>(sp => sp.GetRequiredService<IHangfireDispatcher>());

            services.AddHostedService<ExecutionFlowStartupService>();

            return services;
        }

        /// <summary>
        /// Registers a producer-only ExecutionFlow dispatcher that publishes to the <see cref="JobStorage"/> resolved from the
        /// container, without affecting any other Hangfire configuration in the process. Also registers an
        /// <see cref="IExecutionManager"/> bound to the same storage.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">An optional callback to configure <see cref="HangfireOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="AddHangfireToExecutionFlow"/> was already called.</exception>
        public static IServiceCollection AddExecutionFlowDispatcher(
            this IServiceCollection services,
            Action<HangfireOptions> configure = null)
        {
            return AddExecutionFlowDispatcher(services, sp => sp.GetRequiredService<JobStorage>(), configure);
        }

        /// <summary>
        /// Registers a producer-only ExecutionFlow dispatcher that publishes to the given <see cref="JobStorage"/>, without
        /// affecting any other Hangfire configuration in the process. Also registers an <see cref="IExecutionManager"/>
        /// bound to the same storage. Cancelling jobs from a producer-only host fires no lifecycle hooks.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="storageCall">A factory that returns the storage to publish to.</param>
        /// <param name="configure">An optional callback to configure <see cref="HangfireOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="AddHangfireToExecutionFlow"/> was already called.</exception>
        public static IServiceCollection AddExecutionFlowDispatcher(
            this IServiceCollection services,
            Func<IServiceProvider, JobStorage> storageCall,
            Action<HangfireOptions> configure = null)
        {
            if (services.Any(d => d.ServiceType == typeof(FullModeMarker)))
                throw new InvalidOperationException(
                    "ExecutionFlow is already registered in full mode (AddHangfireToExecutionFlow), which also publishes. " +
                    "Remove the AddExecutionFlowDispatcher call. Use AddExecutionFlowDispatcher alone in apps that don't run handlers.");

            services.AddSingleton<ProducerOnlyModeMarker>();

            var setup = new HangfireSetup();
            if (configure != null)
                setup.Configure(configure);

            services.AddSingleton(typeof(IJobIdGenerator), setup.Options.JobIdGeneratorType);
            services.AddSingleton(typeof(IHangfireJobName), sp =>
                ActivatorUtilities.CreateInstance(sp, setup.Options.JobNameType, setup));
            services.AddSingleton<IExecutionFlowRegistry>(setup);

            services.AddSingleton(sp =>
                setup.BuildDispatcherOnly(storageCall(sp), sp));

            services.AddSingleton(sp =>
            {
                sp.GetRequiredService<IEventDispatcher>();
                return setup.ExecutionManager;
            });

            return services;
        }

        /// <summary>
        /// Builds ExecutionFlow now: registers the Hangfire filters and recurring jobs (full mode) or the dispatcher
        /// (producer-only). Apps running a generic host don't need it, because it runs at startup; call it in apps without
        /// one (e.g. a console app that builds the provider itself).
        /// </summary>
        /// <param name="serviceProvider">The built service provider.</param>
        /// <returns>The same provider for chaining.</returns>
        public static IServiceProvider StartExecutionFlow(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            serviceProvider.GetRequiredService<IEventDispatcher>();
            return serviceProvider;
        }

        private sealed class FullModeMarker
        {
        }

        private sealed class ProducerOnlyModeMarker
        {
        }

        /// <summary>Runs <see cref="StartExecutionFlow"/> at host startup, so recurring jobs and filters are registered early.</summary>
        private sealed class ExecutionFlowStartupService : IHostedService
        {
            private readonly IServiceProvider _serviceProvider;

            public ExecutionFlowStartupService(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _serviceProvider.StartExecutionFlow();
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
