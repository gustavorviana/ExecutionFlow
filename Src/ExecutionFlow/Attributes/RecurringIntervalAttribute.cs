using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Marks a handler as a recurring job that runs every fixed amount of time (e.g. every 15 minutes), as an alternative
    /// to a cron expression. Apply to classes implementing <see cref="Abstractions.IHandler"/>, without
    /// <see cref="RecurringAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefer <see cref="RecurringAttribute"/> with a cron expression when running on Hangfire: an interval with an exact
    /// cron equivalent (one that divides an hour, or a day in whole hours) is registered as that cron anyway, aligned to the
    /// UTC clock, and any other interval needs a per-minute check, with about one minute of precision.
    /// </para>
    /// <para>
    /// An interval means "every N of time", not a time of day, so it has no time zone. The minimum is one minute.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RecurringIntervalAttribute : Attribute
    {
        /// <summary>
        /// Gets the interval as written, in the invariant <see cref="TimeSpan"/> format <c>[d.]hh:mm:ss</c>
        /// (e.g. "00:15:00" for 15 minutes, "10:00:00" for 10 hours, "1.00:00:00" for one day).
        /// </summary>
        public string Interval { get; }

        /// <summary>
        /// Gets or sets an explicit, stable recurring job ID. When not set, the ID comes from the configured
        /// job ID generator (by default the handler type's full name), so renaming the class changes the ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="RecurringIntervalAttribute"/>.
        /// </summary>
        /// <param name="interval">The interval in the invariant <see cref="TimeSpan"/> format <c>[d.]hh:mm:ss</c>, at least one minute.</param>
        public RecurringIntervalAttribute(string interval)
        {
            Interval = interval;
        }
    }
}
