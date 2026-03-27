namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Provides logging capabilities during handler execution.
    /// </summary>
    public interface IExecutionLogger
    {
        /// <summary>
        /// Logs a message with the specified severity level.
        /// </summary>
        /// <param name="level">The log severity level.</param>
        /// <param name="message">The message or format string.</param>
        /// <param name="args">Optional format arguments.</param>
        void Log(HandlerLogType level, string message, params object[] args);
    }
}
