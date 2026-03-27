namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Creates <see cref="IExecutionLogger"/> instances from flow parameters.
    /// </summary>
    public interface IExecutionLoggerFactory
    {
        /// <summary>
        /// Creates a logger for the current execution context.
        /// </summary>
        /// <param name="parameters">The flow parameters for the current execution.</param>
        /// <returns>An <see cref="IExecutionLogger"/> instance, or <c>null</c> if the factory cannot create one.</returns>
        IExecutionLogger CreateLogger(FlowParameters parameters);
    }
}
