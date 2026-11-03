using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Common;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Default implementation of <see cref="IHangfireJobName"/>: ExecutionFlow's naming steps
    /// (<see cref="JobDisplayNameResolver"/>) plus Hangfire's <see cref="JobDisplayNameAttribute"/>, which comes right after
    /// the job's custom name and before the handler class's <see cref="System.ComponentModel.DisplayNameAttribute"/>.
    /// </summary>
    public class DefaultHangfireJobName : JobDisplayNameResolver, IHangfireJobName
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DefaultHangfireJobName"/>.
        /// </summary>
        /// <param name="idGenerator">Generates the fallback name when no name is configured.</param>
        /// <param name="jobExecutionFlow">The handler registry to look up display names.</param>
        public DefaultHangfireJobName(IJobIdGenerator idGenerator, IExecutionFlowRegistry jobExecutionFlow)
            : base(idGenerator, jobExecutionFlow)
        {
        }

        /// <summary>
        /// Gets the display name for a job, in this order: the custom name, Hangfire's <see cref="JobDisplayNameAttribute"/>
        /// (on the handler's <c>HandleAsync</c>, or on a native job's method), the handler class's
        /// <see cref="System.ComponentModel.DisplayNameAttribute"/>, the event class's one when the handler isn't registered,
        /// then the <see cref="IJobIdGenerator"/> fallback.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>The display name, or <c>null</c> if <paramref name="job"/> is <c>null</c>.</returns>
        public override string GetName(Job job)
        {
            if (job == null)
                return null;

            return GetConfiguredName(job)
                ?? GetPlanTriggerName(job)
                ?? GetJobDisplayName(job)
                ?? GetHandlerDisplayName(job)
                ?? GetCarriedTypeDisplayName(job)
                ?? GetFallbackName(job);
        }

        /// <summary>
        /// Hangfire's own naming for a job: <see cref="JobDisplayNameAttribute"/> on the job method, formatted with the
        /// job's arguments, otherwise <c>Type.Method</c>. Used by the dashboard when no <see cref="IHangfireJobName"/> is available.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>The default display name, or <c>null</c> if <paramref name="job"/> is <c>null</c>.</returns>
        internal static string GetHangfireDefaultName(Job job)
        {
            if (job == null)
                return null;

            return FormatJobDisplayName(job.Method, job.Args?.ToArray()) ?? job.ToString();
        }

        /// <summary>
        /// Hangfire's <see cref="JobDisplayNameAttribute"/>: on the handler's <c>HandleAsync</c> for ExecutionFlow jobs
        /// (the attribute only applies to methods, and HandleAsync is the one that runs the job), with <c>{0}</c> = the event;
        /// on the job method for other jobs, with the job's arguments.
        /// </summary>
        private string GetJobDisplayName(Job job)
        {
            var info = HangfireJobInfo.Create(job);
            if (info == null)
                return FormatJobDisplayName(job.Method, job.Args?.ToArray());

            var handleAsync = info.GetHandlerType(Registry)?
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == nameof(IHandler.HandleAsync) && m.IsDefined(typeof(JobDisplayNameAttribute), true));

            var eventArgument = (info as HangfireEventJobInfo)?.Event;
            return FormatJobDisplayName(handleAsync, eventArgument == null ? null : new[] { eventArgument });
        }

        /// <summary>
        /// Reads <see cref="JobDisplayNameAttribute"/> from <paramref name="method"/> and fills its placeholders with
        /// <paramref name="arguments"/>. When a placeholder can't be filled, the text is used as written.
        /// </summary>
        private static string FormatJobDisplayName(MethodInfo method, object[] arguments)
        {
            var attribute = method?.GetCustomAttributes(typeof(JobDisplayNameAttribute), true)
                .OfType<JobDisplayNameAttribute>()
                .FirstOrDefault();

            if (string.IsNullOrEmpty(attribute?.DisplayName))
                return null;

            try
            {
                return string.Format(CultureInfo.CurrentCulture, attribute.DisplayName, arguments ?? Array.Empty<object>());
            }
            catch (FormatException)
            {
                return attribute.DisplayName;
            }
        }
    }
}
