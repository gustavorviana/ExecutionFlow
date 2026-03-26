using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Server;
using System;

namespace ExecutionFlow.Hangfire.Console
{
    internal class HangfireExecutionLoggerFactory : IExecutionLoggerFactory
    {
        private readonly ConsoleConfig _config;

        public HangfireExecutionLoggerFactory(IHangfireOption<ConsoleConfig> option)
        {
            if (option == null) throw new ArgumentNullException(nameof(option));
            _config = option.Value ?? throw new ArgumentNullException(nameof(option), "option.Value cannot be null.");
        }

        public IExecutionLogger CreateLogger(FlowParameters parameters)
        {
            if (parameters.TryGetValue(ContextConsts.Context, out var obj) && obj is PerformContext pc)
                return new HangfireExecutionLogger(pc, _config);

            return null;
        }
    }
}
