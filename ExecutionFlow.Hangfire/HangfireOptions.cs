using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire
{
    public class HangfireOptions : ExecutionFlowOptions
    {
        private readonly List<Type> _stateHandlerTypes = new List<Type>();
        private readonly Dictionary<Type, bool> _jobAutoRun = new Dictionary<Type, bool>();

        public bool AutoRunRecurring { get; set; } = true;

        public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

        internal IReadOnlyDictionary<Type, bool> JobAutoRunSettings => _jobAutoRun;

        public void SetJobAutoRun<T>(bool autoRun)
        {
            SetJobAutoRun(typeof(T), autoRun);
        }

        public void SetJobAutoRun(Type handlerType, bool autoRun)
        {
            ThrowIfLocked();
            _jobAutoRun[handlerType] = autoRun;
        }

        public void AddStateHandler<T>()
        {
            AddStateHandler(typeof(T));
        }

        public void AddStateHandler(Type stateHandlerType)
        {
            ThrowIfLocked();
            _stateHandlerTypes.Add(stateHandlerType);
        }
    }
}
