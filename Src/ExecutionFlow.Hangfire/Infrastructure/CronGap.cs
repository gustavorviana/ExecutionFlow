using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Estimates the smallest gap between two consecutive occurrences of a cron, to warn when it's below Hangfire's polling
    /// interval (specs/015-hosting-second-intervals FR-002, research R-007). Hangfire doesn't expose its cron parser, so this
    /// reads only the seconds and minutes fields.
    /// </summary>
    /// <remarks>
    /// Conservative: it may report a smaller gap than the cron really has (e.g. when the hour field never allows two
    /// consecutive minutes), which only produces a needless warning. It never throws: a cron it can't read gives <c>null</c>.
    /// </remarks>
    internal static class CronGap
    {
        /// <summary>Gets the smallest gap between consecutive occurrences, or <c>null</c> when the cron can't be read.</summary>
        public static TimeSpan? MinimumGap(string cron)
        {
            if (string.IsNullOrWhiteSpace(cron))
                return null;

            var fields = cron.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 5 && fields.Length != 6)
                return null;

            var seconds = fields.Length == 6 ? ParseField(fields[0]) : new List<int> { 0 };
            var minutes = ParseField(fields[fields.Length == 6 ? 1 : 0]);
            if (seconds == null || minutes == null)
                return null;

            // Smallest gap between allowed minutes, around the hour; a single minute repeats at best an hour later.
            var minuteGap = minutes.Count == 1 ? 60 : CyclicMinimum(minutes, 60);

            // Within a minute, between allowed seconds; across minutes, from the last second of one to the first of the next.
            var gap = minuteGap * 60 - seconds[seconds.Count - 1] + seconds[0];
            for (var i = 1; i < seconds.Count; i++)
                gap = Math.Min(gap, seconds[i] - seconds[i - 1]);

            return TimeSpan.FromSeconds(gap);
        }

        private static int CyclicMinimum(List<int> values, int period)
        {
            var gap = period - values[values.Count - 1] + values[0];
            for (var i = 1; i < values.Count; i++)
                gap = Math.Min(gap, values[i] - values[i - 1]);
            return gap;
        }

        // Reads a 0-59 field: *, n, a-b, lists, and steps (*/s, a-b/s, a/s). Anything else gives null.
        private static List<int> ParseField(string field)
        {
            var values = new SortedSet<int>();
            foreach (var part in field.Split(','))
            {
                var stepParts = part.Split('/');
                if (stepParts.Length > 2)
                    return null;

                var step = 1;
                if (stepParts.Length == 2 && (!TryParse(stepParts[1], out step) || step < 1))
                    return null;

                int from, to;
                var range = stepParts[0];
                if (range == "*")
                {
                    from = 0;
                    to = 59;
                }
                else if (range.Contains("-"))
                {
                    var bounds = range.Split('-');
                    if (bounds.Length != 2 || !TryParse(bounds[0], out from) || !TryParse(bounds[1], out to) || from > to)
                        return null;
                }
                else
                {
                    if (!TryParse(range, out from))
                        return null;
                    to = stepParts.Length == 2 ? 59 : from;
                }

                if (from < 0 || to > 59)
                    return null;

                for (var value = from; value <= to; value += step)
                    values.Add(value);
            }

            return values.Count == 0 ? null : values.ToList();
        }

        private static bool TryParse(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
