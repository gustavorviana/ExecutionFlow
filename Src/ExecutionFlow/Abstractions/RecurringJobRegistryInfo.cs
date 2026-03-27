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

        /// <summary>Gets the cron expression for scheduling, or <c>null</c> if triggered manually only.</summary>
        public string Cron { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringJobRegistryInfo"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="cron">The cron expression, or <c>null</c>.</param>
        public RecurringJobRegistryInfo(Type handlerType, string displayName, string cron)
        {
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
            DisplayName = displayName;
            Cron = cron;
        }
    }
}
