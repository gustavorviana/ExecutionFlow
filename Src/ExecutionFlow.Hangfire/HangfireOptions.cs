using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire
{
    public class HangfireOptions : ExecutionFlowOptions
    {
        private readonly List<Type> _stateHandlerTypes = new List<Type>();
        private readonly Dictionary<Type, bool> _jobAutoRun = new Dictionary<Type, bool>();
        internal Type JobNameType { get; private set; } = typeof(DefaultHangfireJobName);
        internal Type JobIdGeneratorType { get; private set; } = typeof(DefaultRecurringServiceIdGenerator);

        internal Dictionary<Type, object> OptionValues { get; } = new Dictionary<Type, object>();

        public bool AutoRunRecurring { get; set; } = true;
        public bool RemoveOrphanRecurringJobs { get; set; } = false;

        public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

        internal IReadOnlyDictionary<Type, bool> JobAutoRunSettings => _jobAutoRun;

        public void SetJobAutoRun<T>(bool autoRun)
        {
            SetJobAutoRun(typeof(T), autoRun);
        }

        public void SetJobAutoRun(Type handlerType, bool autoRun)
        {
            ThrowIfLocked();
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            _jobAutoRun[handlerType] = autoRun;
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
