using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Turns an interval into the single cron expression that runs exactly every interval, when one exists
    /// (specs/012-interval-and-overlap FR-002, specs/015-hosting-second-intervals FR-005).
    /// </summary>
    internal static class IntervalCron
    {
        /// <summary>The cron of a minute ticker: every minute, gated by <see cref="Filters.IntervalTickerFilter"/>.</summary>
        public const string Ticker = "* * * * *";

        /// <summary>The cron of a second ticker: every second (evaluated once per polling cycle), gated the same way.</summary>
        public const string SecondTicker = "* * * * * *";

        /// <summary>Gets whether <paramref name="interval"/> is a whole number of minutes (the minute path of 012).</summary>
        public static bool IsWholeMinutes(TimeSpan interval)
        {
            return interval.Ticks % TimeSpan.TicksPerMinute == 0;
        }

        /// <summary>
        /// Gets the exact cron for <paramref name="interval"/>: whole seconds that divide a minute (6-field cron), whole
        /// minutes that divide an hour, or whole hours that divide a day. Any other interval (45 s, 90 s, 7 min, 90 min, 5 h,
        /// 2 days) has no single cron equivalent.
        /// </summary>
        public static bool TryGetExactCron(TimeSpan interval, out string cron)
        {
            cron = null;

            if (interval.Ticks <= 0)
                return false;

            if (!IsWholeMinutes(interval))
            {
                if (interval.Ticks % TimeSpan.TicksPerSecond != 0 || interval >= TimeSpan.FromMinutes(1))
                    return false;

                var seconds = (long)interval.TotalSeconds;
                if (60 % seconds != 0)
                    return false;

                cron = seconds == 1 ? "* * * * * *" : $"*/{seconds} * * * * *";
                return true;
            }

            var minutes = (long)interval.TotalMinutes;
            if (minutes < 60)
            {
                if (60 % minutes != 0)
                    return false;

                cron = minutes == 1 ? "* * * * *" : $"*/{minutes} * * * *";
                return true;
            }

            if (minutes % 60 != 0)
                return false;

            var hours = minutes / 60;
            if (hours > 24 || 24 % hours != 0)
                return false;

            cron = hours == 1 ? "0 * * * *" : hours == 24 ? "0 0 * * *" : $"0 */{hours} * * *";
            return true;
        }

        /// <summary>
        /// Gets the cron to register for an interval: its exact cron, else the <see cref="SecondTicker"/> for an interval with
        /// a seconds part, else the minute <see cref="Ticker"/>.
        /// </summary>
        public static string ToCron(TimeSpan interval)
        {
            if (TryGetExactCron(interval, out var cron))
                return cron;

            return IsWholeMinutes(interval) ? Ticker : SecondTicker;
        }
    }
}
