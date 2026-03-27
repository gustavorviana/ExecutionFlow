using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Execution context for event handlers, providing access to the event, logger, parameters, and custom ID.
    /// </summary>
    /// <typeparam name="TEvent">The event type being handled.</typeparam>
    public class FlowContext<TEvent> : FlowContext, IDisposable
    {
        /// <summary>Gets the event instance being processed.</summary>
        public TEvent Event { get; }

        /// <summary>Gets the custom identifier for this execution, if set.</summary>
        public string CustomId { get; private set; }
        private Action<string> OnCustomIdChange;

        /// <summary>
        /// Initializes a new instance of <see cref="FlowContext{TEvent}"/>.
        /// </summary>
        /// <param name="parameters">The flow parameters for this execution.</param>
        /// <param name="log">The logger instance.</param>
        /// <param name="event">The event being processed.</param>
        /// <param name="onCustomIdChange">Optional callback invoked when the custom ID changes.</param>
        public FlowContext(FlowParameters parameters,
            IExecutionLogger log,
            TEvent @event,
            Action<string> onCustomIdChange = null)
            : base(parameters, log)
        {
            Event = @event;
            OnCustomIdChange = onCustomIdChange;
        }

        /// <summary>
        /// Sets the custom identifier for this execution and notifies the underlying storage.
        /// </summary>
        /// <param name="id">The custom identifier value.</param>
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

    /// <summary>
    /// Base execution context for recurring handlers, providing access to the logger and parameters.
    /// </summary>
    public class FlowContext
    {
        /// <summary>Gets the logger for this execution.</summary>
        public IExecutionLogger Log { get; }

        /// <summary>Gets the parameters for this execution. Infrastructure keys are read-only; custom keys can be added freely.</summary>
        public FlowParameters Parameters { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="FlowContext"/>.
        /// </summary>
        /// <param name="parameters">The flow parameters.</param>
        /// <param name="log">The logger instance.</param>
        public FlowContext(FlowParameters parameters, IExecutionLogger log)
        {
            Log = log;
            Parameters = parameters;
        }
    }
}
