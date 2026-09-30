using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Marks a handler as a recurring job with the specified cron schedule.
    /// Apply to classes implementing <see cref="Abstractions.IHandler"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RecurringAttribute : Attribute
    {
        /// <summary>Gets the cron expression (e.g., "*/5 * * * *" for every 5 minutes).</summary>
        public string Cron { get; }

        /// <summary>
        /// Gets or sets an explicit, stable recurring job ID. When not set, the ID comes from the configured
        /// job ID generator (by default the handler type's full name), so renaming the class changes the ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the time zone ID the cron expression is evaluated in (e.g. "America/Sao_Paulo").
        /// When not set, the global default applies (UTC unless configured).
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringAttribute"/>.
        /// </summary>
        /// <param name="cron">The cron expression for scheduling.</param>
        public RecurringAttribute(string cron)
        {
            Cron = cron;
        }
    }
}
