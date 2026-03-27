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
        /// Initializes a new instance of <see cref="RecurringAttribute"/>.
        /// </summary>
        /// <param name="cron">The cron expression for scheduling.</param>
        public RecurringAttribute(string cron)
        {
            Cron = cron;
        }
    }
}
