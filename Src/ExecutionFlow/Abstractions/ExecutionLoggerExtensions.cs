using System;

namespace ExecutionFlow.Abstractions
{
    public static class ExecutionLoggerExtensions
    {
        // Trace
        public static void Trace(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Trace, message);

        public static void Trace(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Trace, message, args);

        public static void Trace(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Trace, message?.ToString());

        // Debug
        public static void Debug(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Debug, message);

        public static void Debug(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Debug, message, args);

        public static void Debug(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Debug, message?.ToString());

        // Information
        public static void Info(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Information, message);

        public static void Info(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Information, message, args);

        public static void Info(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Information, message?.ToString());

        // Warning
        public static void Warning(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Warning, message);

        public static void Warning(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Warning, message, args);

        public static void Warning(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Warning, message?.ToString());

        // Error
        public static void Error(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Error, message);

        public static void Error(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Error, message, args);

        public static void Error(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Error, message?.ToString());

        public static void Error(this IExecutionLogger logger, Exception exception)
            => logger.Log(HandlerLogType.Error, exception?.ToString());

        // Success
        public static void Success(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Success, message);

        public static void Success(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Success, message, args);

        public static void Success(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Success, message?.ToString());

        // Critical
        public static void Critical(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Critical, message);

        public static void Critical(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Critical, message, args);

        public static void Critical(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Critical, message?.ToString());

        public static void Critical(this IExecutionLogger logger, Exception exception)
            => logger.Log(HandlerLogType.Critical, exception?.ToString());
    }
}
