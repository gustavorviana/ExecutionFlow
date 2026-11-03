using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Registration metadata for a recurring handler.
    /// </summary>
    public class RecurringJobRegistryInfo : IJobRegistryInfo
    {
        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the display name for dashboard and logging.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the cron expression from <see cref="Attributes.RecurringAttribute"/>, or <c>null</c> when the handler has no
        /// <see cref="Attributes.RecurringAttribute"/>, which is a configuration error for scheduling backends.
        /// </summary>
        public string Cron { get; }

        /// <summary>Gets the explicit recurring job ID from <see cref="Attributes.RecurringAttribute.Id"/>, or <c>null</c>.</summary>
        public string Id { get; }

        /// <summary>Gets the time zone ID from <see cref="Attributes.RecurringAttribute.TimeZone"/>, or <c>null</c>.</summary>
        public string TimeZone { get; }

        /// <summary>
        /// Gets the interval from <see cref="Attributes.RecurringIntervalAttribute"/>, or <c>null</c> when the handler is
        /// scheduled by cron. A handler has either a <see cref="Cron"/> or an <see cref="Interval"/>, never both.
        /// </summary>
        public TimeSpan? Interval { get; }

        /// <summary>
        /// Gets how long a run waits for a running one from <see cref="Attributes.NoOverlapAttribute"/>, or <c>null</c> when
        /// runs may overlap. In the execution plan, <see cref="ExecutionPlanStep.OverlapTimeout"/> takes precedence.
        /// </summary>
        public TimeSpan? OverlapTimeout { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringJobRegistryInfo"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="cron">The cron expression, or <c>null</c>.</param>
        public RecurringJobRegistryInfo(Type handlerType, string displayName, string cron)
            : this(handlerType, displayName, cron, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringJobRegistryInfo"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="cron">The cron expression, or <c>null</c>.</param>
        /// <param name="id">The explicit recurring job ID, or <c>null</c>.</param>
        /// <param name="timeZone">The time zone ID, or <c>null</c>.</param>
        public RecurringJobRegistryInfo(Type handlerType, string displayName, string cron, string id, string timeZone)
            : this(handlerType, displayName, cron, id, timeZone, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringJobRegistryInfo"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="cron">The cron expression, or <c>null</c>.</param>
        /// <param name="id">The explicit recurring job ID, or <c>null</c>.</param>
        /// <param name="timeZone">The time zone ID, or <c>null</c>.</param>
        /// <param name="interval">The interval, or <c>null</c>.</param>
        /// <param name="overlapTimeout">How long a run waits for a running one, or <c>null</c> when runs may overlap.</param>
        /// <exception cref="ArgumentException">Both <paramref name="cron"/> and <paramref name="interval"/> are set.</exception>
        public RecurringJobRegistryInfo(Type handlerType, string displayName, string cron, string id, string timeZone, TimeSpan? interval,
            TimeSpan? overlapTimeout)
        {
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
            if (interval.HasValue && !string.IsNullOrEmpty(cron))
                throw new ArgumentException($"Recurring handler '{handlerType.FullName}' has both a cron and an interval.", nameof(interval));

            DisplayName = displayName;
            Cron = cron;
            Id = string.IsNullOrEmpty(id) ? null : id;
            TimeZone = string.IsNullOrEmpty(timeZone) ? null : timeZone;
            Interval = interval;
            OverlapTimeout = overlapTimeout;
        }
    }
}
