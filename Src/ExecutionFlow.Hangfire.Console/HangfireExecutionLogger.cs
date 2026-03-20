using ExecutionFlow.Abstractions;
using Hangfire.Console;
using Hangfire.Server;
using System;

namespace ExecutionFlow.Hangfire.Console
{
    public class HangfireExecutionLogger : IExecutionLogger
    {
        private readonly PerformContext _performContext;
        private readonly ConsoleConfig _config;

        public HangfireExecutionLogger(PerformContext performContext, ConsoleConfig config)
        {
            _performContext = performContext ?? throw new ArgumentNullException(nameof(performContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Log(HandlerLogType level, string message, params object[] args)
        {
            var color = _config.GetColor(level);
            var formattedMessage = _config.FormatMessage(level, message, args);
            _performContext.WriteLine(color, formattedMessage);
        }
    }
}
