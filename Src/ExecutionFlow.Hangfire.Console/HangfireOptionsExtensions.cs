using System;

namespace ExecutionFlow.Hangfire.Console
{
    public static class HangfireOptionsExtensions
    {
        public static HangfireOptions ConfigureConsole(this HangfireOptions options)
        {
            options.AddOption(new ConsoleConfig());
            options.AddLogger<HangfireExecutionLoggerFactory>();
            return options;
        }

        public static HangfireOptions ConfigureConsole(this HangfireOptions options, Action<ConsoleConfig> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var config = new ConsoleConfig();
            configure(config);
            options.AddOption(config);
            options.AddLogger<HangfireExecutionLoggerFactory>();
            return options;
        }
    }
}
