using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire
{
    public class HangfireOptions : ExecutionFlowOptions
    {
        private readonly List<Type> _stateHandlerTypes = new List<Type>();
        internal Dictionary<Type, bool> RecurringAutoRun { get; } = new Dictionary<Type, bool>();
        internal Type JobNameType { get; private set; } = typeof(DefaultHangfireJobName);
        internal Type JobIdGeneratorType { get; private set; } = typeof(DefaultRecurringServiceIdGenerator);

        public bool GlobalRecurringAutoRun { get; set; } = true;
        internal Dictionary<Type, object> OptionValues { get; } = new Dictionary<Type, object>();

        public bool RemoveOrphanRecurringJobs { get; set; } = false;

        public bool DisableRecurringRetries { get; set; } = true;

        public DeduplicationBehavior DeduplicationBehavior { get; set; } = DeduplicationBehavior.Disabled;

        public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

        public void SetJobAutoRun<T>(bool autoRun)
        {
            SetJobAutoRun(typeof(T), autoRun);
        }

        public void SetJobAutoRun(Type handlerType, bool autoRun)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            RecurringAutoRun[handlerType] = autoRun;
        }

        public void AddStateHandler<T>()
        {
            AddStateHandler(typeof(T));
        }

        public void AddStateHandler(Type stateHandlerType)
        {
            ThrowIfLocked();
            if (stateHandlerType == null) throw new ArgumentNullException(nameof(stateHandlerType));
            _stateHandlerTypes.Add(stateHandlerType);
        }


        public void SetJobName<T>()
        {
            SetJobName(typeof(T));
        }

        public void SetJobName(Type jobNameType)
        {
            ThrowIfLocked();
            if (jobNameType == null) throw new ArgumentNullException(nameof(jobNameType));
            if (!typeof(IHangfireJobName).IsAssignableFrom(jobNameType))
                throw new ArgumentException($"Type '{jobNameType.FullName}' does not implement IHangfireJobName.", nameof(jobNameType));
            JobNameType = jobNameType;
        }

        public void SetJobIdGeneratorType<T>()
        {
            SetJobIdGeneratorType(typeof(T));
        }

        public void SetJobIdGeneratorType(Type jobIdGeneratorType)
        {
            ThrowIfLocked();
            if (jobIdGeneratorType == null) throw new ArgumentNullException(nameof(jobIdGeneratorType));
            if (!typeof(IJobIdGenerator).IsAssignableFrom(jobIdGeneratorType))
                throw new ArgumentException($"Type '{jobIdGeneratorType.FullName}' does not implement IJobIdGenerator.", nameof(jobIdGeneratorType));
            JobIdGeneratorType = jobIdGeneratorType;
        }

        public void AddOption<T>(T value) where T : class
        {
            ThrowIfLocked();
            if (value == null) throw new ArgumentNullException(nameof(value));
            OptionValues[typeof(IHangfireOption<T>)] = new HangfireOption<T>(value);
        }
    }
}
