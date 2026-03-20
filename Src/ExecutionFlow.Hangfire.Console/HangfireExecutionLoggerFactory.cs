using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Server;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Console
{
    internal class HangfireExecutionLoggerFactory : IExecutionLoggerFactory
    {
        private readonly ConsoleConfig _config;

        public HangfireExecutionLoggerFactory(IHangfireOption<ConsoleConfig> option)
        {
            _config = option.Value;
        }

        public IExecutionLogger CreateLogger(IDictionary<string, object> parameters)
        {
            if (parameters.TryGetValue(ContextConsts.Context, out var obj) && obj is PerformContext pc)
                return new HangfireExecutionLogger(pc, _config);

            return null;
        }
    }
}
