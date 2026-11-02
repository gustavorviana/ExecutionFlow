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
        /// <summary>Explicit <see cref="RecurringJobRegistryInfo.Id"/> &gt; <see cref="IJobIdGenerator.GenerateId"/>.</summary>
        public static string ResolveId(RecurringJobRegistryInfo registration, IJobIdGenerator idGenerator)
        {
            return registration.Id ?? idGenerator.GenerateId(registration.HandlerType);
        }

        /// <summary>
        /// Per-handler option &gt; attribute &gt; global option; <c>null</c> means UTC. An interval has no time zone, so it is
        /// always UTC.
        /// </summary>
        public static string ResolveTimeZoneId(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            if (registration.Interval.HasValue)
                return null;

            if (options.RecurringTimeZones.TryGetValue(registration.HandlerType, out var configured))
                return configured;

            return registration.TimeZone ?? options.RecurringTimeZone;
        }

        public static TimeZoneInfo ResolveTimeZone(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            var timeZoneId = ResolveTimeZoneId(registration, options);
            return timeZoneId == null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        /// <summary>The cron to register on schedule: the attribute's cron, or the interval's exact cron or ticker.</summary>
        public static string ResolveCron(RecurringJobRegistryInfo registration)
        {
            return registration.Interval.HasValue ? IntervalCron.ToCron(registration.Interval.Value) : registration.Cron;
        }

        /// <summary>Per-handler option (also set by <see cref="HangfireOptions.UsePlan"/>) &gt; <see cref="HangfireOptions.GlobalRecurringAutoRun"/>.</summary>
        public static bool IsAutoRun(Type handlerType, HangfireOptions options)
        {
            return options.RecurringAutoRun.TryGetValue(handlerType, out var autoRun)
                ? autoRun
                : options.GlobalRecurringAutoRun;
        }
    }
}
