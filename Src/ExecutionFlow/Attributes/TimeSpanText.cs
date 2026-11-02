using System;
using System.Globalization;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Parses the <c>[d.]hh:mm:ss</c> durations written in attributes (<see cref="RecurringIntervalAttribute"/>,
    /// <see cref="DependentRunAttribute"/>).
    /// </summary>
    internal static class TimeSpanText
    {
        private static readonly string[] Formats = { @"hh\:mm\:ss", @"d\.hh\:mm\:ss" };

        /// <summary>
        /// Parses <paramref name="value"/>. Only <c>hh:mm:ss</c> with optional days is accepted: a bare number such as "10"
        /// would otherwise be read as days. A negative value doesn't match the formats.
        /// </summary>
        public static bool TryParse(string value, out TimeSpan duration)
        {
            return TimeSpan.TryParseExact(value ?? string.Empty, Formats, CultureInfo.InvariantCulture, out duration);
        }
    }
}
