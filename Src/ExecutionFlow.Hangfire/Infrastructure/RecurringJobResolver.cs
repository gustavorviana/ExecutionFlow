using ExecutionFlow.Abstractions;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Single place that resolves a recurring handler's job ID, time zone and whether it runs on schedule,
    /// so registration and <see cref="IRecurringTrigger.Trigger(Type)"/> can't disagree.
    /// </summary>
    internal static class RecurringJobResolver
    {
        /// <summary>
        /// Explicit <see cref="RecurringJobRegistryInfo.Id"/> (validated when the handler is registered) &gt;
        /// <see cref="IJobIdGenerator.GenerateId"/>, whose result must match the ID format (specs/016-id-format-validation FR-004).
        /// </summary>
        public static string ResolveId(RecurringJobRegistryInfo registration, IJobIdGenerator idGenerator)
        {
            if (registration.Id != null)
                return registration.Id;

            var id = idGenerator.GenerateId(registration.HandlerType);
            IdFormat.ValidateRecurring(registration.HandlerType, id, nameof(idGenerator));
            return id;
        }

        /// <summary>
        /// Plan step (or, outside the plan, GetPlanOf) &gt; attribute &gt; global option; <c>null</c> means UTC. An interval has no
        /// time zone, so it is always UTC.
        /// </summary>
        public static string ResolveTimeZoneId(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            return options.TimeZoneIdOf(registration);
        }

        public static TimeZoneInfo ResolveTimeZone(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            var timeZoneId = ResolveTimeZoneId(registration, options);
            return timeZoneId == null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        /// <summary>The cron to register on schedule: the attribute's cron, or the interval's exact cron or ticker.</summary>
        public static string ResolveCron(RecurringJobRegistryInfo registration)
        {
            return ResolveCron(registration, interval => interval);
        }

        /// <summary>
        /// The cron to register on schedule, with the interval Hangfire actually runs
        /// (<see cref="HangfireSetup.EffectiveIntervalOf"/>, specs/015-hosting-second-intervals FR-005).
        /// </summary>
        public static string ResolveCron(RecurringJobRegistryInfo registration, Func<TimeSpan, TimeSpan> effectiveInterval)
        {
            return registration.Interval.HasValue ? IntervalCron.ToCron(effectiveInterval(registration.Interval.Value)) : registration.Cron;
        }

        /// <summary>
        /// A handler in the plan: its step's resolved <see cref="ExecutionPlanStep.Enabled"/> (builder &gt; option &gt; global,
        /// with the plan's missing-prerequisite policy). Otherwise GetPlanOf's value &gt; <see cref="HangfireOptions.GlobalRecurringAutoRun"/>.
        /// </summary>
        public static bool IsAutoRun(Type handlerType, HangfireOptions options)
        {
            return options.IsAutoRun(handlerType);
        }
    }
}
