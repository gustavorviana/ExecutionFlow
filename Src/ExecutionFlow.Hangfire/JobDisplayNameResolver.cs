using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;
using System;
using System.ComponentModel;
using System.Linq;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Resolves the display name of a job with ExecutionFlow's fixed naming rules, and falls back to
    /// <see cref="IJobIdGenerator"/> when no name is configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DisplayNameAttribute"/> goes on classes (the handler, or the event); each naming step is a protected method,
    /// so a derived class can override <see cref="GetName"/> and place its own steps between them, as
    /// <see cref="DefaultHangfireJobName"/> does with Hangfire's <c>[JobDisplayName]</c> on the handler's <c>HandleAsync</c>.
    /// </para>
    /// </remarks>
    public class JobDisplayNameResolver
    {
        private readonly IJobIdGenerator _idGenerator;

        /// <summary>Gets the handler registry used to look up handlers and their display names.</summary>
        protected IExecutionFlowRegistry Registry { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="JobDisplayNameResolver"/>.
        /// </summary>
        /// <param name="idGenerator">Generates the fallback name when no name is configured.</param>
        /// <param name="registry">The handler registry to look up display names.</param>
        public JobDisplayNameResolver(IJobIdGenerator idGenerator, IExecutionFlowRegistry registry)
        {
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Gets the display name for a job, in this order: <see cref="GetConfiguredName"/>, <see cref="GetHandlerDisplayName"/>,
        /// <see cref="GetCarriedTypeDisplayName"/>, then <see cref="GetFallbackName"/>.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>The display name, or <c>null</c> if <paramref name="job"/> is <c>null</c>.</returns>
        public virtual string GetName(Job job)
        {
            if (job == null)
                return null;

            return GetConfiguredName(job)
                ?? GetPlanTriggerName(job)
                ?? GetHandlerDisplayName(job)
                ?? GetCarriedTypeDisplayName(job)
                ?? GetFallbackName(job);
        }

        /// <summary>
        /// Gets the name configured for this specific job: the custom name stored when it was published
        /// (<see cref="ICustomNameEvent"/>), or <c>null</c>. Always <c>null</c> for jobs that aren't ExecutionFlow's.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        protected string GetConfiguredName(Job job)
        {
            var customName = (HangfireJobInfo.Create(job) as HangfireEventJobInfo)?.CustomJobName;
            return string.IsNullOrEmpty(customName) ? null : customName;
        }

        /// <summary>
        /// Gets the <see cref="DisplayNameAttribute"/> on the registered handler class, or <c>null</c> when the handler isn't
        /// registered or has none. Always <c>null</c> for jobs that aren't ExecutionFlow's.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        protected string GetHandlerDisplayName(Job job)
        {
            var handler = HangfireJobInfo.Create(job)?.GetHandler(Registry);
            if (handler?.HandlerType == null)
                return null;

            return GetPlanDisplayName(handler.HandlerType) ?? GetExplicitDisplayName(handler);
        }

        /// <summary>
        /// Names the internal job that fires a trigger postponed by a dependent's minimum interval after the dependent, e.g.
        /// "Product Sync (postponed trigger)", instead of the dispatcher type. <c>null</c> for any other job.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        protected string GetPlanTriggerName(Job job)
        {
            if (job?.Type != typeof(HangfireJobDispatcher) || job.Method.Name != nameof(HangfireJobDispatcher.TriggerPlanDependent))
                return null;

            var dependentId = job.Args?.Count > 1 ? job.Args[1] as string : null;
            if (string.IsNullOrEmpty(dependentId))
                return null;

            var registration = Registry.RecurringHandlers.Values
                .FirstOrDefault(r => RecurringJobResolver.ResolveId(r, _idGenerator) == dependentId);

            var dependentName = registration == null
                ? dependentId
                : GetPlanDisplayName(registration.HandlerType) ?? GetExplicitDisplayName(registration) ?? dependentId;

            return dependentName + " (postponed trigger)";
        }

        /// <summary>
        /// The execution plan step's display name (<see cref="ExecutionPlanEntry.DisplayName(string)"/>), when the setup has a
        /// plan with this handler and the name isn't just the class name.
        /// </summary>
        private string GetPlanDisplayName(Type handlerType)
        {
            var plan = (Registry as HangfireSetup)?.Options?.Plan;
            if (plan == null || !plan.TryGet(handlerType, out var step))
                return null;

            return step.DisplayName != handlerType.Name ? step.DisplayName : null;
        }

        /// <summary>
        /// Gets the <see cref="DisplayNameAttribute"/> on the class the job carries (the event, or the recurring handler) when
        /// the handler isn't registered on this host (e.g. producer-only), or <c>null</c>. Always <c>null</c> for jobs that
        /// aren't ExecutionFlow's.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        protected string GetCarriedTypeDisplayName(Job job)
        {
            var info = HangfireJobInfo.Create(job);
            if (info == null || info.GetHandler(Registry)?.HandlerType != null)
                return null;

            return GetDisplayNameAttribute(info.CarriedType);
        }

        /// <summary>
        /// Gets the name used when nothing names the job: <see cref="IJobIdGenerator.GenerateId"/> of the registered handler
        /// type, otherwise of the type the job carries (event or recurring handler type), otherwise of <see cref="Job.Type"/>.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        protected string GetFallbackName(Job job)
        {
            var info = HangfireJobInfo.Create(job);
            var type = info?.GetHandlerType(Registry) ?? info?.CarriedType ?? job.Type;
            return _idGenerator.GenerateId(type);
        }

        /// <summary>
        /// The registered display name is the handler's <see cref="DisplayNameAttribute"/> (set by
        /// <c>ExecutionFlowOptions.Add</c>), or the class name when there is none; only a name other than
        /// the class name counts as configured.
        /// </summary>
        private static string GetExplicitDisplayName(IJobRegistryInfo handler)
        {
            return !string.IsNullOrEmpty(handler.DisplayName) && handler.DisplayName != handler.HandlerType.Name
                ? handler.DisplayName
                : null;
        }

        private static string GetDisplayNameAttribute(Type type)
        {
            if (type == null)
                return null;

            var attribute = (DisplayNameAttribute)Attribute.GetCustomAttribute(type, typeof(DisplayNameAttribute));
            return string.IsNullOrEmpty(attribute?.DisplayName) ? null : attribute.DisplayName;
        }
    }
}
