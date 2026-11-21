using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace ExecutionFlow.Hosting
{
    /// <summary>Extension methods for running ExecutionFlow recurring handlers inside the .NET Generic Host.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the local host: it runs the recurring handlers (<see cref="IHandler"/>) on their cron or interval inside
        /// this app, respecting the execution plan with the same meaning as Hangfire, without a job server or a storage. With
        /// <see cref="HostingOptions.RunOnce"/>, it runs every handler once and stops the application instead. Event handlers
        /// found by <c>Scan</c> are ignored.
        /// </summary>
        /// <remarks>
        /// The local host runs <b>locally and isolated</b>. It never communicates with another app or process using it (unlike
        /// Hangfire servers sharing a storage), and two registrations in the same process (separate service collections) don't
        /// coordinate either: each has its own plan cycles, overlap guards, concurrency limit and schedulers, so a handler
        /// registered in both runs in both. Plan cycles live in memory and start from zero on every start, so after a restart a
        /// dependent waits for a new cycle of its prerequisites. Intervals count from the host start, unlike Hangfire's
        /// clock-aligned exact crons.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures <see cref="HostingOptions"/>.</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// It was already called on <paramref name="services"/>, a handler has no schedule, a time zone isn't found, or
        /// <c>GetPlanOf</c> names a type that isn't a registered recurring handler.
        /// </exception>
        public static IServiceCollection AddExecutionFlowHosting(this IServiceCollection services, Action<HostingOptions> configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (services.Any(d => d.ServiceType == typeof(HostingMarker)))
                throw new InvalidOperationException("AddExecutionFlowHosting was already called on this service collection.");

            var setup = new HostingSetup();
            setup.Configure(configure ?? (_ => { }));

            services.AddSingleton<HostingMarker>();

            foreach (var handlerType in setup.RecurringOrder)
                services.AddScoped(handlerType);

            foreach (var loggerFactoryType in setup.LoggerFactoryTypes)
                services.AddSingleton(typeof(IExecutionLoggerFactory), loggerFactoryType);

            services.TryAddSingleton<ExecutionLoggerFactory>();

            if (setup.Plan != null)
                services.AddSingleton(setup.Plan);

            services.AddSingleton(sp => new HostingRuntime(setup, sp));
            services.AddSingleton<IExecutionPlanRunner>(sp => sp.GetRequiredService<HostingRuntime>().Runner);

            if (setup.Options.RunOnce)
            {
                services.AddSingleton<IHostedService>(sp =>
                {
                    var runtime = sp.GetRequiredService<HostingRuntime>();
                    return new RunOnceHostedService(runtime.Runner, sp.GetRequiredService<IHostApplicationLifetime>(), runtime.Logger);
                });

                return services;
            }

            // Registered first, so it stops last: it cancels the runs still in progress.
            services.AddSingleton<IHostedService>(sp =>
            {
                var runtime = sp.GetRequiredService<HostingRuntime>();
                return new StartupReportHostedService(setup, runtime.Coordinator, runtime.Logger);
            });

            foreach (var handlerType in setup.RecurringOrder)
            {
                if (!setup.IsEnabled(handlerType) || setup.IsTriggeredByPlan(handlerType))
                    continue;

                var registration = setup.RecurringHandlers[handlerType];
                services.AddSingleton<IHostedService>(sp =>
                {
                    var runtime = sp.GetRequiredService<HostingRuntime>();
                    return new RecurringHandlerHost(registration, setup, runtime.Coordinator, runtime.Clock, runtime.Logger);
                });
            }

            return services;
        }

        private sealed class HostingMarker
        {
        }
    }
}
