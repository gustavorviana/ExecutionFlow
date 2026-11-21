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
        private string _jobId;
        private int _attemptNumber = 1;
        private Type _handlerType;
        private Type _eventType;
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
            return WithJob(new FlowContext<TEvent>(_parameters, CreateLogger(), @event, onCustomIdChange));
        }

        /// <summary>
        /// Builds a <see cref="FlowContext"/> for a recurring handler.
        /// </summary>
        /// <returns>A configured <see cref="FlowContext"/>.</returns>
        public FlowContext Build()
        {
            ThrowIfBuilt();
            _built = true;
            return WithJob(new FlowContext(_parameters, CreateLogger()));
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
        /// Sets the processor's job facts exposed as <see cref="FlowContext.JobId"/> and <see cref="FlowContext.AttemptNumber"/>.
        /// </summary>
        /// <param name="jobId">The processor's job ID.</param>
        /// <param name="attemptNumber">The attempt being executed, starting at 1.</param>
        /// <returns>This builder for chaining.</returns>
        public FlowContextBuilder SetJob(string jobId, int attemptNumber)
        {
            ThrowIfBuilt();
            if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber), "The first attempt is 1.");

            _jobId = jobId;
            _attemptNumber = attemptNumber;
            return this;
        }

        /// <summary>
        /// Sets the handler (and event) running the job, exposed to logger factories through
        /// <see cref="ExecutionLoggerContext"/>.
        /// </summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="eventType">The event type, or <c>null</c> for recurring jobs.</param>
        /// <returns>This builder for chaining.</returns>
        public FlowContextBuilder SetHandler(Type handlerType, Type eventType = null)
        {
            ThrowIfBuilt();
            _handlerType = handlerType;
            _eventType = eventType;
            return this;
        }

        private TContext WithJob<TContext>(TContext context) where TContext : FlowContext
        {
            context.JobId = _jobId;
            context.AttemptNumber = _attemptNumber;
            return context;
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
            return _logFactory.CreateLogger(new ExecutionLoggerContext(_parameters, _jobId, _attemptNumber, _handlerType, _eventType));
        }

        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("FlowContextBuilder has already been built and cannot be modified.");
        }
    }
}
