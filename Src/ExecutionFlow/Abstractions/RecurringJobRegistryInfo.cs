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
        {
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
            DisplayName = displayName;
            Cron = cron;
            Id = string.IsNullOrEmpty(id) ? null : id;
            TimeZone = string.IsNullOrEmpty(timeZone) ? null : timeZone;
        }
    }
}
