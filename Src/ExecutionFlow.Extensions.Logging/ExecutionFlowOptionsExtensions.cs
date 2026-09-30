using System;

namespace ExecutionFlow.Extensions.Logging
{
    /// <summary>
    /// Registration of the <c>Microsoft.Extensions.Logging</c> bridge.
    /// </summary>
    public static class ExecutionFlowOptionsExtensions
    {
        /// <summary>
        /// Sends handler logs (<c>context.Log</c>) to the application's <c>ILogger</c>, in addition to any other logger
        /// (e.g. the Hangfire console). Requires an <c>ILoggerFactory</c> in the container (with DI it comes from
        /// <c>AddLogging</c>/the host; without DI, register one in the setup's activator).
        /// </summary>
        /// <typeparam name="TOptions">The processor's options type.</typeparam>
        /// <param name="options">The options.</param>
        /// <returns>The same options for chaining.</returns>
        public static TOptions AddMicrosoftLogging<TOptions>(this TOptions options) where TOptions : ExecutionFlowOptions
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.AddLogger<MicrosoftExecutionLoggerFactory>();
            return options;
        }
    }
}
