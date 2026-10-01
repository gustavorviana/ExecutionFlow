using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>What <see cref="PrerequisiteGenerationStore.DecideTrigger"/> decided for a dependent.</summary>
    internal enum TriggerDecision
    {
        /// <summary>Nothing to do: a prerequisite has no new cycle, the dependent is running, or a trigger is already pending.</summary>
        None,

        /// <summary>Trigger the dependent now. The plan's trigger marker is already set.</summary>
        TriggerNow,

        /// <summary>The minimum interval hasn't elapsed: schedule a trigger for the due time, which is already recorded.</summary>
        ScheduleTrigger
    }

    /// <summary>How a new job of a dependent was created, as <see cref="PrerequisiteGenerationStore.OnDependentCreated"/> sees it.</summary>
    internal enum DependentRunOrigin
    {
        /// <summary>Triggered by the plan (a prerequisite completed, or a postponed trigger fired).</summary>
        Plan,

        /// <summary>An occurrence of the dependent's own schedule (<c>RunOnOwnSchedule</c>).</summary>
        Schedule,

        /// <summary>Anything else: the dashboard, or code calling <c>Trigger</c>.</summary>
        Manual
    }

    /// <summary>
    /// The execution plan's state, kept in the job storage so every server agrees on it (ADR-0009).
    /// </summary>
    /// <remarks>
    /// <para>A generation is how many cycles a prerequisite completed: a counter rather than a flag, because the rule covers
    /// every run of the dependent, not only the first. Hash <c>executionflow:plan:generation</c> has one field per recurring
    /// job ID; hash <c>executionflow:plan:consumed:&lt;dependent id&gt;</c> has the generation of each prerequisite the
    /// dependent last used; hash <c>executionflow:plan:state:&lt;dependent id&gt;</c> has the dependent's pending trigger,
    /// active job, last finish and postponed trigger.</para>
    /// <para>Every read-then-write runs under one distributed lock: two servers finishing at once would otherwise lose an
    /// increment or trigger a dependent twice.</para>
    /// </remarks>
    internal static class PrerequisiteGenerationStore
    {
        internal const string GenerationKey = "executionflow:plan:generation";
        internal const string ConsumedKeyPrefix = "executionflow:plan:consumed:";
        internal const string StateKeyPrefix = "executionflow:plan:state:";
        internal const string LockResource = "executionflow:plan-lock";

        internal const string TriggeredField = "Triggered";
        internal const string JobIdField = "JobId";
        internal const string FinishedAtField = "FinishedAt";
        internal const string ScheduledForField = "ScheduledFor";

        internal static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

        public static string GetConsumedKey(string dependentId) => ConsumedKeyPrefix + dependentId;

        public static string GetStateKey(string dependentId) => StateKeyPrefix + dependentId;

        /// <summary>Records a new completed cycle of <paramref name="jobId"/>.</summary>
        public static void Increment(IStorageConnection connection, string jobId)
        {
            using (connection.AcquireDistributedLock(LockResource, LockTimeout))
            {
                var generation = ReadLong(connection.GetAllEntriesFromHash(GenerationKey), jobId) + 1;
                Write(connection, GenerationKey, new Dictionary<string, string> { [jobId] = Format(generation) });
            }
        }

        /// <summary>
        /// Decides whether to trigger a dependent now, postpone it to the end of <paramref name="minInterval"/>, or do nothing,
        /// and records the decision (pending trigger marker, or postponed due time) under the same lock.
        /// </summary>
        public static TriggerDecision DecideTrigger(IStorageConnection connection, string dependentId, IReadOnlyList<string> prerequisiteIds,
            TimeSpan? minInterval, DateTime utcNow, out DateTime dueAt)
        {
            dueAt = utcNow;

            using (connection.AcquireDistributedLock(LockResource, LockTimeout))
            {
                var generations = connection.GetAllEntriesFromHash(GenerationKey);
                var consumed = connection.GetAllEntriesFromHash(GetConsumedKey(dependentId));
                if (!prerequisiteIds.All(id => ReadLong(generations, id) > ReadLong(consumed, id)))
                    return TriggerDecision.None;

                var state = connection.GetAllEntriesFromHash(GetStateKey(dependentId));

                // A running dependent is re-checked when it finishes, so runs never overlap.
                var activeJobId = Read(state, JobIdField);
                if (activeJobId != null && DeduplicationStore.IsActive(connection, activeJobId))
                    return TriggerDecision.None;

                if (Read(state, TriggeredField) != null)
                    return TriggerDecision.None;

                if (minInterval.HasValue && ReadLong(state, FinishedAtField) > 0)
                {
                    dueAt = new DateTime(ReadLong(state, FinishedAtField), DateTimeKind.Utc) + minInterval.Value;
                    if (utcNow < dueAt)
                    {
                        if (Read(state, ScheduledForField) != null)
                            return TriggerDecision.None;

                        Write(connection, GetStateKey(dependentId), new Dictionary<string, string> { [ScheduledForField] = Format(dueAt.Ticks) });
                        return TriggerDecision.ScheduleTrigger;
                    }
                }

                Write(connection, GetStateKey(dependentId), new Dictionary<string, string> { [TriggeredField] = "1" });
                return TriggerDecision.TriggerNow;
            }
        }

        /// <summary>Clears the pending trigger marker, when triggering failed after <see cref="DecideTrigger"/>.</summary>
        public static void ClearTriggered(IStorageConnection connection, string dependentId)
        {
            SetFields(connection, dependentId, new Dictionary<string, string> { [TriggeredField] = string.Empty });
        }

        /// <summary>Clears the postponed trigger, when it fires.</summary>
        public static void ClearScheduled(IStorageConnection connection, string dependentId)
        {
            SetFields(connection, dependentId, new Dictionary<string, string> { [ScheduledForField] = string.Empty });
        }

        /// <summary>Records that the dependent's run finished (successfully or not): it's no longer active, and its minimum interval starts.</summary>
        public static void RecordFinished(IStorageConnection connection, string dependentId, DateTime utcNow)
        {
            SetFields(connection, dependentId, new Dictionary<string, string>
            {
                [JobIdField] = string.Empty,
                [FinishedAtField] = Format(utcNow.Ticks)
            });
        }

        /// <summary>
        /// Classifies a new job of a dependent and applies the gate. A plan trigger or a schedule occurrence runs only if every
        /// prerequisite has a new generation; a manual run always runs. A job that runs consumes the current generations and
        /// becomes the dependent's active job. Returns <c>false</c>, with the pending prerequisites, when the job must not run.
        /// </summary>
        public static bool OnDependentCreated(IStorageConnection connection, string dependentId, IReadOnlyList<string> prerequisiteIds,
            string jobId, bool isScheduleOccurrence, out DependentRunOrigin origin, out IReadOnlyList<string> pending)
        {
            using (connection.AcquireDistributedLock(LockResource, LockTimeout))
            {
                var state = connection.GetAllEntriesFromHash(GetStateKey(dependentId));
                origin = Read(state, TriggeredField) != null
                    ? DependentRunOrigin.Plan
                    : isScheduleOccurrence ? DependentRunOrigin.Schedule : DependentRunOrigin.Manual;

                var generations = connection.GetAllEntriesFromHash(GenerationKey);
                var consumed = connection.GetAllEntriesFromHash(GetConsumedKey(dependentId));

                pending = origin == DependentRunOrigin.Manual
                    ? (IReadOnlyList<string>)new string[0]
                    : prerequisiteIds.Where(id => ReadLong(generations, id) <= ReadLong(consumed, id)).ToList();

                var stateUpdate = new Dictionary<string, string>();
                if (origin == DependentRunOrigin.Plan)
                    stateUpdate[TriggeredField] = string.Empty;

                if (pending.Count == 0)
                {
                    Write(connection, GetConsumedKey(dependentId), prerequisiteIds.ToDictionary(id => id, id => Format(ReadLong(generations, id))));
                    stateUpdate[JobIdField] = jobId;
                }

                if (stateUpdate.Count > 0)
                    Write(connection, GetStateKey(dependentId), stateUpdate);

                return pending.Count == 0;
            }
        }

        private static void SetFields(IStorageConnection connection, string dependentId, Dictionary<string, string> values)
        {
            using (connection.AcquireDistributedLock(LockResource, LockTimeout))
                Write(connection, GetStateKey(dependentId), values);
        }

        /// <summary>A missing or empty field reads as <c>null</c> (Hangfire can't remove a single hash field, so fields are cleared to "").</summary>
        private static string Read(Dictionary<string, string> entries, string field)
        {
            return entries != null && entries.TryGetValue(field, out var value) && !string.IsNullOrEmpty(value) ? value : null;
        }

        private static long ReadLong(Dictionary<string, string> entries, string field)
        {
            return long.TryParse(Read(entries, field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
        }

        private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Write(IStorageConnection connection, string key, Dictionary<string, string> values)
        {
            using (var transaction = connection.CreateWriteTransaction())
            {
                transaction.SetRangeInHash(key, values);
                transaction.Commit();
            }
        }
    }
}
