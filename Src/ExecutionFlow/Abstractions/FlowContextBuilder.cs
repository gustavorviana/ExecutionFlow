using System;

namespace ExecutionFlow.Abstractions
{
    public class FlowContextBuilder
    {
        private readonly ExecutionLoggerFactory _logFactory;
        private readonly FlowParameters _parameters = new FlowParameters();
        private bool _built;

        public FlowContextBuilder(ExecutionLoggerFactory logFactory)
        {
            _logFactory = logFactory ?? throw new ArgumentNullException(nameof(logFactory));
        }

        public FlowContext<TEvent> Build<TEvent>(TEvent @event, Action<string> onCustomIdChange)
        {
            ThrowIfBuilt();
            _built = true;
            return new FlowContext<TEvent>(_parameters, CreateLogger(), @event, onCustomIdChange);
        }

        public FlowContext Build()
        {
            ThrowIfBuilt();
            _built = true;
            return new FlowContext(_parameters, CreateLogger());
        }

        public FlowContextBuilder AddReadOnly(string key, object value)
        {
            ThrowIfBuilt();
            _parameters.AddReadOnly(key, value);
            return this;
        }

        public FlowContextBuilder Add(string key, object value)
        {
            ThrowIfBuilt();
            _parameters.Add(key, value);
            return this;
        }

        private IExecutionLogger CreateLogger()
        {
            return _logFactory.CreateLogger(_parameters);
        }

        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("FlowContextBuilder has already been built and cannot be modified.");
        }
    }
}
