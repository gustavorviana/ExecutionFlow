using System;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// Extension methods for <see cref="HangfireOptions"/> that add Hangfire Console logging support.
    /// </summary>
    public static class HangfireOptionsExtensions
    {
        /// <summary>
        /// Enables Hangfire Console logging with default configuration.
        /// </summary>
        /// <param name="options">The Hangfire options to configure.</param>
        /// <returns>The options instance for chaining.</returns>
        public static HangfireOptions ConfigureConsole(this HangfireOptions options)
        {
            options.AddOption(new ConsoleConfig());
            options.AddLogger<HangfireExecutionLoggerFactory>();
            return options;
        }

        /// <summary>
        /// Enables Hangfire Console logging with custom configuration.
        /// </summary>
        /// <param name="options">The Hangfire options to configure.</param>
        /// <param name="configure">An action to customize the <see cref="ConsoleConfig"/>.</param>
        /// <returns>The options instance for chaining.</returns>
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
