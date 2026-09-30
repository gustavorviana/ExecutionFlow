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

        /// <summary>Per-handler option &gt; attribute &gt; global option; <c>null</c> means UTC.</summary>
        public static string ResolveTimeZoneId(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            if (options.RecurringTimeZones.TryGetValue(registration.HandlerType, out var configured))
                return configured;

            return registration.TimeZone ?? options.RecurringTimeZone;
        }

        public static TimeZoneInfo ResolveTimeZone(RecurringJobRegistryInfo registration, HangfireOptions options)
        {
            var timeZoneId = ResolveTimeZoneId(registration, options);
            return timeZoneId == null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        /// <summary>Per-handler option &gt; <see cref="HangfireOptions.GlobalRecurringAutoRun"/>.</summary>
        public static bool IsAutoRun(Type handlerType, HangfireOptions options)
        {
            return options.RecurringAutoRun.TryGetValue(handlerType, out var autoRun)
                ? autoRun
                : options.GlobalRecurringAutoRun;
        }
    }
}
