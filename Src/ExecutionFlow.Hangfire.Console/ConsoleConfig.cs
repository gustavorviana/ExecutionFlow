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

        /// <summary>
        /// Gets or sets an optional custom formatter for log messages. When <c>null</c>, the default format is used:
        /// <c>[LEVEL] message</c>, with placeholders filled like <c>ILogger</c> templates (each <c>{name}</c> takes the
        /// argument in the same position).
        /// </summary>
        public Func<HandlerLogType, string, object[], string> Formatter { get; set; }

        /// <summary>
        /// Gets or sets the lowest level written to the console. Default is <see cref="HandlerLogType.Trace"/> (everything).
        /// <see cref="HandlerLogType.Success"/> is compared as <see cref="HandlerLogType.Information"/>.
        /// </summary>
        public HandlerLogType MinimumLevel { get; set; } = HandlerLogType.Trace;

        internal bool IsEnabled(HandlerLogType level)
        {
            var effective = level == HandlerLogType.Success ? HandlerLogType.Information : level;
            var minimum = MinimumLevel == HandlerLogType.Success ? HandlerLogType.Information : MinimumLevel;
            return effective >= minimum;
        }

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

            return $"[{level.ToString().ToUpperInvariant()}] {LogMessageTemplate.Format(message, args)}";
        }
    }
}
