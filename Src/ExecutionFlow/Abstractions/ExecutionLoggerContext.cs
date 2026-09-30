using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// What a logger factory knows about the execution it creates a logger for.
    /// Received by <see cref="IExecutionLoggerContextFactory.CreateLogger(ExecutionLoggerContext)"/>.
    /// </summary>
    public sealed class ExecutionLoggerContext
    {
        /// <summary>Gets the execution's parameters (the same instance the handler sees).</summary>
        public FlowParameters Parameters { get; }

        /// <summary>Gets the processor's job ID, or <c>null</c> outside a processor.</summary>
        public string JobId { get; }

        /// <summary>Gets the attempt being executed, starting at 1.</summary>
        public int AttemptNumber { get; }

        /// <summary>Gets the handler type running the job, or <c>null</c> when unknown.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the event type for event jobs, or <c>null</c> for recurring jobs.</summary>
        public Type EventType { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionLoggerContext"/>.
        /// </summary>
        public ExecutionLoggerContext(FlowParameters parameters, string jobId, int attemptNumber, Type handlerType, Type eventType)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            JobId = jobId;
            AttemptNumber = attemptNumber;
            HandlerType = handlerType;
            EventType = eventType;
        }
    }

    /// <summary>
    /// A logger factory that needs more than the parameters: the job and handler facts. Optional; factories that only
    /// implement <see cref="IExecutionLoggerFactory"/> keep receiving the parameters.
    /// </summary>
    public interface IExecutionLoggerContextFactory : IExecutionLoggerFactory
    {
        /// <summary>
        /// Creates a logger for one execution, or returns <c>null</c> to skip this execution.
        /// </summary>
        /// <param name="context">The execution's facts.</param>
        IExecutionLogger CreateLogger(ExecutionLoggerContext context);
    }
}
