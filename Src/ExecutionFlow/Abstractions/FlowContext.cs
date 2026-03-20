using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class FlowContext<TEvent> : FlowContext, IDisposable
    {
        public TEvent Event { get; }

        public string CustomId { get; private set; }
        private Action<string> OnCustomIdChange;

        public FlowContext(IReadOnlyDictionary<string, object> parameters,
            IExecutionLogger log,
            TEvent @event,
            Action<string> onCustomIdChange = null)
            : base(parameters, log)
        {
            Event = @event;
            OnCustomIdChange = onCustomIdChange;
        }

        public void SetCustomId(string id)
        {
            CustomId = id;
            OnCustomIdChange?.Invoke(id);
        }

        void IDisposable.Dispose()
        {
            OnCustomIdChange = null;
        }
    }

    public class FlowContext
    {
        public IExecutionLogger Log { get; }

        public IReadOnlyDictionary<string, object> Parameters { get; }

        public FlowContext(IReadOnlyDictionary<string, object> parameters, IExecutionLogger log)
        {
            Log = log;
            Parameters = parameters;
        }
    }
}
