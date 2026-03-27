using ExecutionFlow.Abstractions;
using Hangfire.Console;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// Configuration for the Hangfire Console integration, including log level colors and custom message formatting.
    /// </summary>
    public class ConsoleConfig
    {
        private readonly Dictionary<HandlerLogType, ConsoleTextColor> _colors = new Dictionary<HandlerLogType, ConsoleTextColor>
        {
            { HandlerLogType.Trace, ConsoleTextColor.DarkGray },
            { HandlerLogType.Debug, ConsoleTextColor.Gray },
            { HandlerLogType.Information, ConsoleTextColor.White },
            { HandlerLogType.Warning, ConsoleTextColor.Yellow },
            { HandlerLogType.Error, ConsoleTextColor.Red },
            { HandlerLogType.Critical, ConsoleTextColor.DarkRed },
            { HandlerLogType.Success, ConsoleTextColor.Green }
        };

        /// <summary>Gets or sets an optional custom formatter for log messages. When <c>null</c>, the default format is used.</summary>
        public Func<HandlerLogType, string, object[], string> Formatter { get; set; }

        /// <summary>
        /// Gets the console text color associated with the specified log level.
        /// </summary>
        /// <param name="logType">The log level.</param>
        /// <returns>The <see cref="ConsoleTextColor"/> for the log level.</returns>
        public ConsoleTextColor GetColor(HandlerLogType logType)
        {
            return _colors.TryGetValue(logType, out var color) ? color : ConsoleTextColor.White;
        }

        /// <summary>
        /// Sets the console text color for a specific log level.
        /// </summary>
        /// <param name="logType">The log level.</param>
        /// <param name="color">The color to use.</param>
        public void SetColor(HandlerLogType logType, ConsoleTextColor color)
        {
            _colors[logType] = color;
        }

        internal string FormatMessage(HandlerLogType level, string message, object[] args)
        {
            if (Formatter != null)
                return Formatter(level, message, args);

            string formattedMessage;
            try
            {
                formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
            }
            catch (FormatException)
            {
                formattedMessage = message;
            }

            return $"[{level.ToString().ToUpperInvariant()}] {formattedMessage}";
        }
    }
}
