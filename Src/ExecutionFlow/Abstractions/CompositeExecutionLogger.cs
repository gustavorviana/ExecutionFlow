using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Forwards each message to every configured logger. Each logger runs in isolation: a logger that throws never
    /// affects the handler or the other loggers, because logging must not change a job's outcome.
    /// </summary>
    internal class CompositeExecutionLogger : IExecutionLogger
    {
        private readonly IReadOnlyList<IExecutionLogger> _loggers;

        public CompositeExecutionLogger(IReadOnlyList<IExecutionLogger> loggers)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        public void Log(HandlerLogType level, string message, params object[] args)
        {
            for (var i = 0; i < _loggers.Count; i++)
            {
                try
                {
                    _loggers[i].Log(level, message, args);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("ExecutionFlow: Logger '{0}' failed: {1}", _loggers[i].GetType().FullName, ex.Message);
                }
            }
        }
    }
}
