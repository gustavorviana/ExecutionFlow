using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class FlowContext<TEvent> : FlowContext, IDisposable
    {
        public TEvent Event { get; }

        public string CustomId { get; private set; }
        private Action<string> OnCustomIdChange;

        public FlowContext(IExecutionLogger log, TEvent @event, Action<string> onCustomIdChange = null)
            : base(log)
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

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        public FlowContext(IExecutionLogger log)
        {
            Log = log;
        }
    }
}
