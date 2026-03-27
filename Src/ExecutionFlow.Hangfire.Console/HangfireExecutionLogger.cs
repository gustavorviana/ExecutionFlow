using ExecutionFlow.Abstractions;
using Hangfire.Console;
using Hangfire.Server;
using System;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// An <see cref="IExecutionLogger"/> implementation that writes colored log messages to the Hangfire Console.
    /// </summary>
    public class HangfireExecutionLogger : IExecutionLogger
    {
        private readonly PerformContext _performContext;
        private readonly ConsoleConfig _config;

        /// <summary>
        /// Initializes a new instance with the specified Hangfire perform context and console configuration.
        /// </summary>
        /// <param name="performContext">The Hangfire perform context for writing to the console.</param>
        /// <param name="config">The console configuration for colors and formatting.</param>
        public HangfireExecutionLogger(PerformContext performContext, ConsoleConfig config)
        {
            _performContext = performContext ?? throw new ArgumentNullException(nameof(performContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Writes a formatted, color-coded log message to the Hangfire Console.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="message">The message format string.</param>
        /// <param name="args">Optional format arguments.</param>
        public void Log(HandlerLogType level, string message, params object[] args)
        {
            var color = _config.GetColor(level);
            var formattedMessage = _config.FormatMessage(level, message, args);
            _performContext.WriteLine(color, formattedMessage);
        }
    }
}
