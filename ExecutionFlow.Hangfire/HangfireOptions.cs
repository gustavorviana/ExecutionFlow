using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire
{
    public class HangfireOptions
    {
        private readonly List<Type> _stateHandlerTypes = new List<Type>();
        private readonly Dictionary<Type, bool> _jobAutoRun = new Dictionary<Type, bool>();
        private bool _locked;

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

        internal void Lock()
        {
            _locked = true;
        }

        private void ThrowIfLocked()
        {
            if (_locked)
                throw new InvalidOperationException("HangfireOptions cannot be modified after Configure has completed.");
        }
    }
}
