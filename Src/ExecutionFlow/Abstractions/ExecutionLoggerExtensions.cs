using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Convenience extension methods for <see cref="IExecutionLogger"/> that provide typed log-level methods.
    /// </summary>
    public static class ExecutionLoggerExtensions
    {
        /// <summary>Logs a trace-level message.</summary>
        public static void Trace(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Trace, message);

        /// <summary>Logs a trace-level formatted message.</summary>
        public static void Trace(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Trace, message, args);

        /// <summary>Logs a trace-level message from an object's string representation.</summary>
        public static void Trace(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Trace, message?.ToString());

        /// <summary>Logs a debug-level message.</summary>
        public static void Debug(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Debug, message);

        /// <summary>Logs a debug-level formatted message.</summary>
        public static void Debug(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Debug, message, args);

        /// <summary>Logs a debug-level message from an object's string representation.</summary>
        public static void Debug(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Debug, message?.ToString());

        /// <summary>Logs an informational message.</summary>
        public static void Info(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Information, message);

        /// <summary>Logs an informational formatted message.</summary>
        public static void Info(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Information, message, args);

        /// <summary>Logs an informational message from an object's string representation.</summary>
        public static void Info(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Information, message?.ToString());

        /// <summary>Logs a warning message.</summary>
        public static void Warning(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Warning, message);

        /// <summary>Logs a warning formatted message.</summary>
        public static void Warning(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Warning, message, args);

        /// <summary>Logs a warning message from an object's string representation.</summary>
        public static void Warning(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Warning, message?.ToString());

        /// <summary>Logs an error message.</summary>
        public static void Error(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Error, message);

        /// <summary>Logs an error formatted message.</summary>
        public static void Error(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Error, message, args);

        /// <summary>Logs an error message from an object's string representation.</summary>
        public static void Error(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Error, message?.ToString());

        /// <summary>Logs an exception at error level.</summary>
        public static void Error(this IExecutionLogger logger, Exception exception)
            => logger.Log(HandlerLogType.Error, exception?.ToString());

        /// <summary>Logs a success message.</summary>
        public static void Success(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Success, message);

        /// <summary>Logs a success formatted message.</summary>
        public static void Success(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Success, message, args);

        /// <summary>Logs a success message from an object's string representation.</summary>
        public static void Success(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Success, message?.ToString());

        /// <summary>Logs a critical message.</summary>
        public static void Critical(this IExecutionLogger logger, string message)
            => logger.Log(HandlerLogType.Critical, message);

        /// <summary>Logs a critical formatted message.</summary>
        public static void Critical(this IExecutionLogger logger, string message, params object[] args)
            => logger.Log(HandlerLogType.Critical, message, args);

        /// <summary>Logs a critical message from an object's string representation.</summary>
        public static void Critical(this IExecutionLogger logger, object message)
            => logger.Log(HandlerLogType.Critical, message?.ToString());

        /// <summary>Logs an exception at critical level.</summary>
        public static void Critical(this IExecutionLogger logger, Exception exception)
            => logger.Log(HandlerLogType.Critical, exception?.ToString());
    }
}
