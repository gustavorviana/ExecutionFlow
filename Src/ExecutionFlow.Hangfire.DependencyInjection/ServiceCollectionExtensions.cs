using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hangfire.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHangfireToExecutionFlow(
            this IServiceCollection services,
            Action<HangfireOptions> configure = null)
        {
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
            services.AddSingleton(typeof(IHangfireJobName), setup.Options.JobNameType);
            services.AddSingleton<IExecutionFlowRegistry>(setup);

            services.AddSingleton(sp =>
            {
                var jobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var jobStorage = sp.GetRequiredService<JobStorage>();

                return setup.Build(jobClient, jobStorage, sp);
            });

            services.AddSingleton<IExecutionManager>(sp =>
            {
                var jobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var jobStorage = sp.GetRequiredService<JobStorage>();
                return new HangfireExecutionManager(jobClient, jobStorage);
            });

            services.AddSingleton<IRecurringTrigger>(sp =>
            {
                var jobStorage = sp.GetRequiredService<JobStorage>();
                var jobIdGenerator = sp.GetRequiredService<IJobIdGenerator>();
                var registry = sp.GetRequiredService<IExecutionFlowRegistry>();
                return new HangfireRecurringTrigger(jobStorage, jobIdGenerator, registry);
            });

            services.AddHostedService<HostedDi>();

            return services;
        }

        private class HostedDi : IHostedService
        {
            public HostedDi(IEventDispatcher dispatcher)
            {

            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}