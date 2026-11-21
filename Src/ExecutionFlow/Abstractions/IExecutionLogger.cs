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
        /// <remarks>
        /// The message is an <c>ILogger</c>-style template written by the developer. Pass values that come from outside
        /// (event data, request input) in <paramref name="args"/>, never inside the message: a value inside the message
        /// can break the template (a <c>{</c> or <c>}</c>) or forge log lines (a line break).
        /// </remarks>
        /// <param name="level">The log severity level.</param>
        /// <param name="message">The message template, e.g. <c>"Order {OrderId} processed"</c>.</param>
        /// <param name="args">The values of the template's placeholders, by position.</param>
        void Log(HandlerLogType level, string message, params object[] args);
    }
}
