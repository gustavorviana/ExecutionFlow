using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Reservation keys that map a custom ID to its active job: a Hangfire hash
    /// <c>executionflow:dedup:{customId}</c> with the job ID. A key is trusted only while the job it points to is active.
    /// </summary>
    internal static class DeduplicationStore
    {
        internal const string KeyPrefix = "executionflow:dedup:";
        internal const string LockPrefix = "executionflow:dedup-lock:";
        internal const string JobIdField = "JobId";
        internal const string CreatedAtField = "CreatedAt";

        internal static readonly TimeSpan BaseExpiration = TimeSpan.FromDays(30);

        private static readonly HashSet<string> ActiveStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EnqueuedState.StateName,
            ScheduledState.StateName,
            AwaitingState.StateName,
            ProcessingState.StateName
        };

        private static readonly HashSet<string> ReleaseStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SucceededState.StateName,
            DeletedState.StateName,
            FailedState.StateName
        };

        public static string GetKey(string customId) => KeyPrefix + customId;

        public static string GetLockResource(string customId) => LockPrefix + customId;

        /// <summary>Returns the ID of the job reserved for <paramref name="customId"/>, if any, whatever its state.</summary>
        public static string GetReservedJobId(IStorageConnection connection, string customId)
        {
            var entries = connection.GetAllEntriesFromHash(GetKey(customId));
            return entries != null && entries.TryGetValue(JobIdField, out var jobId) && !string.IsNullOrEmpty(jobId)
                ? jobId
                : null;
        }

        /// <summary>Returns the ID of the active job reserved for <paramref name="customId"/>, or <c>null</c> when the key is missing or stale.</summary>
        public static string FindActiveJobId(IStorageConnection connection, string customId)
        {
            var jobId = GetReservedJobId(connection, customId);
            return jobId != null && IsInState(connection, jobId, ActiveStates) ? jobId : null;
        }

        /// <summary>Returns <c>true</c> when the job exists and is Enqueued, Scheduled, Awaiting or Processing.</summary>
        public static bool IsActive(IStorageConnection connection, string jobId)
        {
            return IsInState(connection, jobId, ActiveStates);
        }

        /// <summary>Returns <c>true</c> when the job's current state is <paramref name="stateName"/>.</summary>
        public static bool IsInState(IStorageConnection connection, string jobId, string stateName)
        {
            return string.Equals(connection.GetStateData(jobId)?.Name, stateName, StringComparison.OrdinalIgnoreCase);
        }

        public static void Reserve(IStorageConnection connection, string customId, string jobId, TimeSpan expireIn)
        {
            var key = GetKey(customId);
            using (var transaction = connection.CreateWriteTransaction())
            {
                transaction.SetRangeInHash(key, new Dictionary<string, string>
                {
                    [JobIdField] = jobId,
                    [CreatedAtField] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                });

                // Storages without JobStorageTransaction keep the key; stale keys are detected on read.
                if (transaction is JobStorageTransaction storageTransaction)
                    storageTransaction.ExpireHash(key, expireIn);

                transaction.Commit();
            }
        }

        public static bool IsReleaseState(string stateName) => stateName != null && ReleaseStates.Contains(stateName);

        /// <summary>Removes the key for <paramref name="customId"/> when it still points to <paramref name="jobId"/>.</summary>
        public static void Release(IStorageConnection connection, IWriteOnlyTransaction transaction, string customId, string jobId)
        {
            if (GetReservedJobId(connection, customId) == jobId)
                transaction.RemoveHash(GetKey(customId));
        }

        private static bool IsInState(IStorageConnection connection, string jobId, HashSet<string> states)
        {
            var stateName = connection.GetStateData(jobId)?.Name;
            return stateName != null && states.Contains(stateName);
        }
    }
}
