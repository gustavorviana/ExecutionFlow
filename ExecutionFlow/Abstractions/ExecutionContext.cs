using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class ExecutionContext<TEvent> : ExecutionContext
    {
        public TEvent Event { get; }

        public string CustomId { get; private set; }

        public ExecutionContext(IExecutionLogger log, TEvent @event)
            : base(log)
        {
            Event = @event;
        }

        public void SetCustomId(string id)
        {
            CustomId = id;
        }
    }

    public class ExecutionContext
    {
        public IExecutionLogger Log { get; }

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        public ExecutionContext(IExecutionLogger log)
        {
            Log = log;
        }
    }
}
