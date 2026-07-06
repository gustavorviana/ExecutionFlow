using Hangfire;
using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Extension methods for <see cref="DashboardOptions"/> that wire ExecutionFlow job names
    /// into the Hangfire dashboard.
    /// </summary>
    public static class DashboardOptionsExtensions
    {
        /// <summary>
        /// Sets <see cref="DashboardOptions.DisplayNameFunc"/> so the dashboard displays
        /// ExecutionFlow handler names (or registered titles) instead of the internal
        /// dispatcher method name.
        /// </summary>
        /// <param name="options">The dashboard options.</param>
        /// <param name="jobName">The job name generator to use.</param>
        /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
        public static DashboardOptions UseExecutionFlowJobNames(this DashboardOptions options, IHangfireJobName jobName)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (jobName == null) throw new ArgumentNullException(nameof(jobName));

            options.DisplayNameFunc = (context, job) => jobName.GetName(job);
            return options;
        }

        /// <summary>
        /// Sets <see cref="DashboardOptions.DisplayNameFunc"/> using the <see cref="IHangfireJobName"/>
        /// resolved from the given service provider. Resolution is deferred until the first
        /// dashboard render, so this can be called before the provider is fully built.
        /// </summary>
        /// <param name="options">The dashboard options.</param>
        /// <param name="serviceProvider">The service provider containing an <see cref="IHangfireJobName"/> registration.</param>
        /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
        public static DashboardOptions UseExecutionFlowJobNames(this DashboardOptions options, IServiceProvider serviceProvider)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            options.DisplayNameFunc = (context, job) =>
            {
                var jobName = (IHangfireJobName)serviceProvider.GetService(typeof(IHangfireJobName));
                if (jobName == null)
                    throw new InvalidOperationException(
                        $"No {nameof(IHangfireJobName)} is registered in the service provider. " +
                        "Register ExecutionFlow via AddHangfireToExecutionFlow/AddExecutionFlowDispatcher or provide an IHangfireJobName instance.");

                return jobName.GetName(job);
            };

            return options;
        }
    }
}
