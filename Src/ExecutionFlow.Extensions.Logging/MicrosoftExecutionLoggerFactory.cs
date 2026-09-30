using ExecutionFlow.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Extensions.Logging
{
    /// <summary>
    /// Sends handler logs (<c>context.Log</c>) to <see cref="ILogger"/>. The category is the handler type (or the event type
    /// when the handler isn't known), and every entry runs inside a scope with <c>JobId</c> and <c>AttemptNumber</c>.
    /// Register it with <see cref="ExecutionFlowOptionsExtensions.AddMicrosoftLogging"/>.
    /// </summary>
    public class MicrosoftExecutionLoggerFactory : IExecutionLoggerContextFactory
    {
        internal const string DefaultCategory = "ExecutionFlow";

        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="MicrosoftExecutionLoggerFactory"/>.
        /// </summary>
        /// <param name="loggerFactory">The application's logger factory.</param>
        public MicrosoftExecutionLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc />
        public IExecutionLogger CreateLogger(ExecutionLoggerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var category = context.HandlerType?.FullName ?? context.EventType?.FullName ?? DefaultCategory;
            var scope = new Dictionary<string, object>
            {
                ["JobId"] = context.JobId,
                ["AttemptNumber"] = context.AttemptNumber
            };

            return new MicrosoftExecutionLogger(_loggerFactory.CreateLogger(category), scope);
        }

        /// <inheritdoc />
        public IExecutionLogger CreateLogger(FlowParameters parameters)
        {
            return CreateLogger(new ExecutionLoggerContext(parameters ?? new FlowParameters(), null, 1, null, null));
        }
    }

    internal sealed class MicrosoftExecutionLogger : IExecutionLogger
    {
        private readonly ILogger _logger;
        private readonly IReadOnlyDictionary<string, object> _scope;

        public MicrosoftExecutionLogger(ILogger logger, IReadOnlyDictionary<string, object> scope)
        {
            _logger = logger;
            _scope = scope;
        }

        public void Log(HandlerLogType level, string message, params object[] args)
        {
            var logLevel = ToLogLevel(level);
            if (!_logger.IsEnabled(logLevel))
                return;

            using (_logger.BeginScope(_scope))
            {
                // The message is passed as a template, so structured logging keeps its named properties.
                _logger.Log(logLevel, message ?? string.Empty, args ?? Array.Empty<object>());
            }
        }

        /// <summary><see cref="HandlerLogType.Success"/> has no Microsoft.Extensions.Logging equivalent: it maps to Information.</summary>
        internal static LogLevel ToLogLevel(HandlerLogType level)
        {
            switch (level)
            {
                case HandlerLogType.Trace: return LogLevel.Trace;
                case HandlerLogType.Debug: return LogLevel.Debug;
                case HandlerLogType.Warning: return LogLevel.Warning;
                case HandlerLogType.Error: return LogLevel.Error;
                case HandlerLogType.Critical: return LogLevel.Critical;
                default: return LogLevel.Information;
            }
        }
    }
}
