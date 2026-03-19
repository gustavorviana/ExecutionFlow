using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ExecutionFlow.Hangfire.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHangfireToExecutionFlow(
            this IServiceCollection services,
            Action<HangfireOptions> configure)
        {
            var setup = new HangfireSetup();
            setup.Configure(configure);

            foreach (var registration in setup.Registrations)
                services.AddTransient(registration.HandlerType);

            services.AddSingleton<IExecutionFlowRegistry>(setup);

            services.AddSingleton(sp =>
            {
                var jobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var jobStorage = sp.GetRequiredService<JobStorage>();
                var jobActivator = sp.GetRequiredService<JobActivator>();

                return setup.Build(jobClient, jobStorage, jobActivator);
            });

            services.AddSingleton<IExecutionManager>(sp =>
            {
                var jobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var jobStorage = sp.GetRequiredService<JobStorage>();
                return new HangfireExecutionManager(jobClient, jobStorage);
            });

            return services;
        }
    }
}