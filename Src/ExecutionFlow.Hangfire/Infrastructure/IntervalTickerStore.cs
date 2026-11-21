using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// When each ticker interval last ran, kept in the job storage so every server agrees on it
    /// (specs/012-interval-and-overlap/research.md, R-001).
    /// </summary>
    /// <remarks>
    /// Hash <c>executionflow:interval:lastrun</c> has one field per recurring job ID, with the start of the minute the last run
    /// started in (UTC ticks). Truncating to the minute keeps a 7-minute interval at 7 minutes even though the scheduler
    /// fires a few seconds into each minute. An interval with a seconds part truncates
    /// to the second instead, in the same format, so a handler that changes path reads a compatible value.
    /// </remarks>
    internal static class IntervalTickerStore
    {
        internal const string LastRunKey = "executionflow:interval:lastrun";
        internal const string LockResource = "executionflow:interval-lock";

        internal static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Decides whether a run of <paramref name="recurringJobId"/> starts now, and records it when it does. A forced run
        /// (a manual trigger) always starts; a scheduled one starts once <paramref name="interval"/> has elapsed since the
        /// last run.
        /// </summary>
        /// <param name="resolution">
        /// What the start is truncated to: one minute for whole-minute intervals (as before), one second for an interval with
        /// a seconds part (specs/015-hosting-second-intervals FR-005). Default is one minute.
        /// </param>
        public static bool TryStartRun(IStorageConnection connection, string recurringJobId, TimeSpan interval, DateTime utcNow, bool force,
            TimeSpan? resolution = null)
        {
            var step = (resolution ?? TimeSpan.FromMinutes(1)).Ticks;
            var minute = new DateTime(utcNow.Ticks - utcNow.Ticks % step, DateTimeKind.Utc);

            using (connection.AcquireDistributedLock(LockResource, LockTimeout))
            {
                if (!force)
                {
                    var entries = connection.GetAllEntriesFromHash(LastRunKey);
                    if (entries != null && entries.TryGetValue(recurringJobId, out var value)
                        && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastRun)
                        && minute.Ticks - lastRun < interval.Ticks)
                        return false;
                }

                using (var transaction = connection.CreateWriteTransaction())
                {
                    transaction.SetRangeInHash(LastRunKey, new Dictionary<string, string>
                    {
                        [recurringJobId] = minute.Ticks.ToString(CultureInfo.InvariantCulture)
                    });
                    transaction.Commit();
                }

                return true;
            }
        }
    }
}
