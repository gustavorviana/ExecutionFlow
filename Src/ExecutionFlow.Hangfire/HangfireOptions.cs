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
        public bool GlobalRecurringAutoRun { get; set; } = true;
        internal Dictionary<Type, object> OptionValues { get; } = new Dictionary<Type, object>();

        /// <summary>Gets or sets whether orphan recurring jobs (not registered in the setup) are automatically removed. Default is <c>false</c>.</summary>
        public bool RemoveOrphanRecurringJobs { get; set; } = false;

        /// <summary>Gets or sets whether automatic retries are disabled for recurring jobs. Default is <c>true</c>.</summary>
        public bool DisableRecurringRetries { get; set; } = true;

        /// <summary>Gets or sets the deduplication behavior for event jobs. Default is <see cref="Hangfire.DeduplicationBehavior.Disabled"/>.</summary>
        public DeduplicationBehavior DeduplicationBehavior { get; set; } = DeduplicationBehavior.Disabled;

        /// <summary>Gets the list of registered state handler types.</summary>
        public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

        /// <summary>
        /// Sets whether a specific recurring handler type should auto-run when enqueued.
        /// </summary>
        /// <typeparam name="T">The handler type.</typeparam>
        /// <param name="autoRun">Whether to auto-run the handler.</param>
        public void SetJobAutoRun<T>(bool autoRun)
        {
            SetJobAutoRun(typeof(T), autoRun);
        }

        /// <summary>
        /// Sets whether a specific recurring handler type should auto-run when enqueued.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="autoRun">Whether to auto-run the handler.</param>
        public void SetJobAutoRun(Type handlerType, bool autoRun)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            RecurringAutoRun[handlerType] = autoRun;
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
