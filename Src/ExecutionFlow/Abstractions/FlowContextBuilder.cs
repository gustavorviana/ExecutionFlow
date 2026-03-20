using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class FlowContextBuilder
    {
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        private readonly ExecutionLoggerFactory _logFactory;

        public FlowContextBuilder(ExecutionLoggerFactory logFactory)
        {
            _logFactory = logFactory;
        }

        public FlowContext<TEvent> Build<TEvent>(TEvent @event, Action<string> onCustomIdChange)
        {
            return new FlowContext<TEvent>(Parameters, CreateLogger(), @event, onCustomIdChange);
        }

        public FlowContext Build()
        {
            return new FlowContext(Parameters, CreateLogger());
        }

        private IExecutionLogger CreateLogger()
        {
            return _logFactory.CreateLogger(Parameters);
        }
    }
}
