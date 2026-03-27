using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Builds a <see cref="FlowContext"/> or <see cref="FlowContext{TEvent}"/> with parameters and logging.
    /// Can only be built once; subsequent calls throw <see cref="InvalidOperationException"/>.
    /// </summary>
    public class FlowContextBuilder
    {
        private readonly ExecutionLoggerFactory _logFactory;
        private readonly FlowParameters _parameters = new FlowParameters();
        private bool _built;

        /// <summary>
        /// Initializes a new instance of <see cref="FlowContextBuilder"/>.
        /// </summary>
        /// <param name="logFactory">The logger factory used to create loggers for the context.</param>
        public FlowContextBuilder(ExecutionLoggerFactory logFactory)
        {
            _logFactory = logFactory ?? throw new ArgumentNullException(nameof(logFactory));
        }

        /// <summary>
        /// Builds a typed <see cref="FlowContext{TEvent}"/> for an event handler.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <param name="onCustomIdChange">Callback invoked when the custom ID is changed via <see cref="FlowContext{TEvent}.SetCustomId"/>.</param>
        /// <returns>A configured <see cref="FlowContext{TEvent}"/>.</returns>
        public FlowContext<TEvent> Build<TEvent>(TEvent @event, Action<string> onCustomIdChange)
        {
            ThrowIfBuilt();
            _built = true;
            return new FlowContext<TEvent>(_parameters, CreateLogger(), @event, onCustomIdChange);
        }

        /// <summary>
        /// Builds a <see cref="FlowContext"/> for a recurring handler.
        /// </summary>
        /// <returns>A configured <see cref="FlowContext"/>.</returns>
        public FlowContext Build()
        {
            ThrowIfBuilt();
            _built = true;
            return new FlowContext(_parameters, CreateLogger());
        }

        /// <summary>
        /// Adds a read-only infrastructure parameter that cannot be modified by handlers.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public FlowContextBuilder AddReadOnly(string key, object value)
        {
            ThrowIfBuilt();
            _parameters.AddReadOnly(key, value);
            return this;
        }

        /// <summary>
        /// Adds a parameter that can be modified by handlers during execution.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>This builder for fluent chaining.</returns>
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
