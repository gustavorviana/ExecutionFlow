using ExecutionFlow.Abstractions;
using Hangfire.Console;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Console
{
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

        public Func<HandlerLogType, string, object[], string> Formatter { get; set; }

        public ConsoleTextColor GetColor(HandlerLogType logType)
        {
            return _colors.TryGetValue(logType, out var color) ? color : ConsoleTextColor.White;
        }

        public void SetColor(HandlerLogType logType, ConsoleTextColor color)
        {
            _colors[logType] = color;
        }

        internal string FormatMessage(HandlerLogType level, string message, object[] args)
        {
            if (Formatter != null)
                return Formatter(level, message, args);

            var formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
            return $"[{level.ToString().ToUpperInvariant()}] {formattedMessage}";
        }
    }
}
