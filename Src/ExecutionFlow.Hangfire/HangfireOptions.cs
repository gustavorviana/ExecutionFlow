using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Configures Hangfire-specific options for ExecutionFlow.
    /// </summary>
    public class HangfireOptions : ExecutionFlowOptions
    {
        private readonly List<Type> _stateHandlerTypes = new List<Type>();
        internal Dictionary<Type, bool> RecurringAutoRun { get; } = new Dictionary<Type, bool>();
        internal Type JobNameType { get; private set; } = typeof(DefaultHangfireJobName);
        internal Type JobIdGeneratorType { get; private set; } = typeof(DefaultRecurringServiceIdGenerator);

        /// <summary>Gets or sets whether recurring jobs auto-start when enqueued. Default is <c>true</c>.</summary>
        public bool GlobalRecurringAutoRun { get => _globalRecurringAutoRun; set { ThrowIfLocked(); _globalRecurringAutoRun = value; } }
        private bool _globalRecurringAutoRun = true;
        internal Dictionary<Type, object> OptionValues { get; } = new Dictionary<Type, object>();

        /// <summary>Gets or sets whether orphan recurring jobs (not registered in the setup) are automatically removed. Default is <c>false</c>.</summary>
        public bool RemoveOrphanRecurringJobs { get => _removeOrphanRecurringJobs; set { ThrowIfLocked(); _removeOrphanRecurringJobs = value; } }
        private bool _removeOrphanRecurringJobs = false;

        /// <summary>Gets or sets whether automatic retries are disabled for recurring jobs. Default is <c>true</c>.</summary>
        public bool DisableRecurringRetries { get => _disableRecurringRetries; set { ThrowIfLocked(); _disableRecurringRetries = value; } }
        private bool _disableRecurringRetries = true;

        /// <summary>
        /// Gets or sets a callback that receives exceptions thrown by lifecycle hooks (<c>IOn*</c> state handlers).
        /// Hook exceptions never affect the job or the other hooks; when this is <c>null</c> (default) they are
        /// written with <see cref="System.Diagnostics.Trace.TraceWarning(string)"/>.
        /// </summary>
        public Action<ExecutionFlow.Abstractions.Events.HookErrorContext> HookErrorHandler { get => _hookErrorHandler; set { ThrowIfLocked(); _hookErrorHandler = value; } }
        private Action<ExecutionFlow.Abstractions.Events.HookErrorContext> _hookErrorHandler;

        /// <summary>
        /// Gets or sets whether event jobs whose event type has no registered handler on the executing host are retried.
        /// Default is <c>false</c>: such jobs fail without automatic retries. Set to <c>true</c> when consumers with
        /// different handlers share a queue, so another consumer can pick the job up on retry.
        /// </summary>
        public bool RetryUnregisteredEventJobs { get => _retryUnregisteredEventJobs; set { ThrowIfLocked(); _retryUnregisteredEventJobs = value; } }
        private bool _retryUnregisteredEventJobs = false;

        /// <summary>
        /// Gets or sets how long a publish waits for the per-custom-ID deduplication lock. Default is 1 second.
        /// The lock is only taken when deduplication is enabled and the event has a non-empty custom ID.
        /// </summary>
        public TimeSpan DeduplicationLockTimeout { get => _deduplicationLockTimeout; set { ThrowIfLocked(); _deduplicationLockTimeout = value; } }
        private TimeSpan _deduplicationLockTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets whether a publish creates the job anyway (without deduplication) when the deduplication lock
        /// isn't acquired within <see cref="DeduplicationLockTimeout"/>. Default is <c>false</c>: a
        /// <see cref="global::Hangfire.Storage.DistributedLockTimeoutException"/> is thrown.
        /// </summary>
        public bool CreateOnDeduplicationLockTimeout { get => _createOnDeduplicationLockTimeout; set { ThrowIfLocked(); _createOnDeduplicationLockTimeout = value; } }
        private bool _createOnDeduplicationLockTimeout = false;

        /// <summary>Gets the list of registered state handler types.</summary>
        public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

        /// <summary>
        /// Sets whether a specific recurring handler runs on its schedule. A handler that doesn't auto-run is registered
        /// with a schedule that never fires, so it only runs when triggered manually. Applies to the whole storage.
        /// </summary>
        /// <typeparam name="T">The handler type.</typeparam>
        /// <param name="autoRun">Whether to run the handler on its schedule.</param>
        public void SetJobAutoRun<T>(bool autoRun)
        {
            SetJobAutoRun(typeof(T), autoRun);
        }

        /// <summary>
        /// Sets whether a specific recurring handler runs on its schedule. A handler that doesn't auto-run is registered
        /// with a schedule that never fires, so it only runs when triggered manually. Applies to the whole storage.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="autoRun">Whether to run the handler on its schedule.</param>
        public void SetJobAutoRun(Type handlerType, bool autoRun)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            RecurringAutoRun[handlerType] = autoRun;
        }

        /// <summary>
        /// Gets or sets the default time zone ID for recurring schedules (e.g. "America/Sao_Paulo").
        /// <c>null</c> (default) means UTC. Overridden by <see cref="ExecutionFlow.Attributes.RecurringAttribute.TimeZone"/>
        /// and by <see cref="SetJobTimeZone(Type, string)"/>.
        /// </summary>
        public string RecurringTimeZone { get => _recurringTimeZone; set { ThrowIfLocked(); _recurringTimeZone = value; } }
        private string _recurringTimeZone;

        internal Dictionary<Type, string> RecurringTimeZones { get; } = new Dictionary<Type, string>();

        /// <summary>
        /// Sets the time zone ID for a specific recurring handler. Takes precedence over the attribute and the global default.
        /// </summary>
        /// <typeparam name="T">The handler type.</typeparam>
        /// <param name="timeZoneId">The time zone ID (e.g. "America/Sao_Paulo").</param>
        public void SetJobTimeZone<T>(string timeZoneId)
        {
            SetJobTimeZone(typeof(T), timeZoneId);
        }

        /// <summary>
        /// Sets the time zone ID for a specific recurring handler. Takes precedence over the attribute and the global default.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="timeZoneId">The time zone ID (e.g. "America/Sao_Paulo").</param>
        public void SetJobTimeZone(Type handlerType, string timeZoneId)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            if (string.IsNullOrEmpty(timeZoneId)) throw new ArgumentNullException(nameof(timeZoneId));
            RecurringTimeZones[handlerType] = timeZoneId;
        }

        /// <summary>
        /// Registers a state handler type to receive job lifecycle notifications.
        /// </summary>
        /// <typeparam name="T">The state handler type.</typeparam>
        public void AddStateHandler<T>()
        {
            AddStateHandler(typeof(T));
        }

        /// <summary>
        /// Registers a state handler type to receive job lifecycle notifications.
        /// </summary>
        /// <param name="stateHandlerType">The state handler type.</param>
        public void AddStateHandler(Type stateHandlerType)
        {
            ThrowIfLocked();
            if (stateHandlerType == null) throw new ArgumentNullException(nameof(stateHandlerType));
            _stateHandlerTypes.Add(stateHandlerType);
        }


        /// <summary>
        /// Sets the custom job name generator type used to display job names in the Hangfire dashboard.
        /// </summary>
        /// <typeparam name="T">A type implementing <see cref="IHangfireJobName"/>.</typeparam>
        public void SetJobName<T>()
        {
            SetJobName(typeof(T));
        }

        /// <summary>
        /// Sets the custom job name generator type used to display job names in the Hangfire dashboard.
        /// </summary>
        /// <param name="jobNameType">A type implementing <see cref="IHangfireJobName"/>.</param>
        public void SetJobName(Type jobNameType)
        {
            ThrowIfLocked();
            if (jobNameType == null) throw new ArgumentNullException(nameof(jobNameType));
            if (!typeof(IHangfireJobName).IsAssignableFrom(jobNameType))
                throw new ArgumentException($"Type '{jobNameType.FullName}' does not implement IHangfireJobName.", nameof(jobNameType));
            JobNameType = jobNameType;
        }

        /// <summary>
        /// Sets the custom job ID generator type used to create recurring job identifiers.
        /// </summary>
        /// <typeparam name="T">A type implementing <see cref="IJobIdGenerator"/>.</typeparam>
        public void SetJobIdGeneratorType<T>()
        {
            SetJobIdGeneratorType(typeof(T));
        }

        /// <summary>
        /// Sets the custom job ID generator type used to create recurring job identifiers.
        /// </summary>
        /// <param name="jobIdGeneratorType">A type implementing <see cref="IJobIdGenerator"/>.</param>
        public void SetJobIdGeneratorType(Type jobIdGeneratorType)
        {
            ThrowIfLocked();
            if (jobIdGeneratorType == null) throw new ArgumentNullException(nameof(jobIdGeneratorType));
            if (!typeof(IJobIdGenerator).IsAssignableFrom(jobIdGeneratorType))
                throw new ArgumentException($"Type '{jobIdGeneratorType.FullName}' does not implement IJobIdGenerator.", nameof(jobIdGeneratorType));
            JobIdGeneratorType = jobIdGeneratorType;
        }

        /// <summary>
        /// Registers a custom option value that can be resolved by Hangfire job handlers at runtime.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="value">The option value.</param>
        public void AddOption<T>(T value) where T : class
        {
            ThrowIfLocked();
            if (value == null) throw new ArgumentNullException(nameof(value));
            OptionValues[typeof(IHangfireOption<T>)] = new HangfireOption<T>(value);
        }
    }
}
