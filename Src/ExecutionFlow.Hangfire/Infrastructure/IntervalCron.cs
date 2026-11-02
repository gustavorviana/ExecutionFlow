using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Turns an interval into the single cron expression that runs exactly every interval, when one exists.
    /// </summary>
    internal static class IntervalCron
    {
        /// <summary>The cron of a ticker: every minute, gated by <see cref="Filters.IntervalTickerFilter"/>.</summary>
        public const string Ticker = "* * * * *";

        /// <summary>
        /// Gets the exact cron for <paramref name="interval"/>: whole minutes that divide an hour, or whole hours that divide
        /// a day. Any other interval (7 min, 90 min, 5 h, 2 days) has no single cron equivalent.
        /// </summary>
        public static bool TryGetExactCron(TimeSpan interval, out string cron)
        {
            cron = null;

            if (interval.Ticks % TimeSpan.TicksPerMinute != 0)
                return false;

            var minutes = (long)interval.TotalMinutes;
            if (minutes <= 0)
                return false;

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

        /// <summary>Gets the cron to register for an interval: its exact cron, or the <see cref="Ticker"/>.</summary>
        public static string ToCron(TimeSpan interval)
        {
            return TryGetExactCron(interval, out var cron) ? cron : Ticker;
        }
    }
}
