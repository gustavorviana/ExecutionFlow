using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
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
                _loggers[i].Log(level, message, args);
        }
    }
}
