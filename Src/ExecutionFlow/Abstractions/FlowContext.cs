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
        /// <remarks>
        /// Obsolete: deduplication only reserves the custom ID given at publish time (<see cref="ICustomIdEvent"/>),
        /// so an ID changed here isn't deduplicated against. Will be removed in 2.0.
        /// </remarks>
        [Obsolete("Set the custom ID on the event through ICustomIdEvent. Deduplication only uses the ID given at publish time. SetCustomId will be removed in 2.0.")]
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

        /// <summary>
        /// Gets the parameters for this execution. Keys added by the infrastructure are read-only;
        /// custom keys can be added freely. Parameters live only for this execution: they aren't persisted, passed to
        /// retries or shared with other jobs, and the logger sees the same instance.
        /// </summary>
        public FlowParameters Parameters { get; }

        /// <summary>Gets the processor's ID of the job being executed, or <c>null</c> when the context wasn't created by a processor.</summary>
        public string JobId { get; internal set; }

        /// <summary>Gets the attempt being executed: 1 for the first run, 2 for the first retry, and so on.</summary>
        public int AttemptNumber { get; internal set; } = 1;

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
