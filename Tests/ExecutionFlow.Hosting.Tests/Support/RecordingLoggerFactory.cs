using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>Counts the execution loggers it creates.</summary>
    internal sealed class RecordingLoggerFactory : IExecutionLoggerFactory
    {
        private int _created;

        public int Created => _created;

        public IExecutionLogger CreateLogger(FlowParameters parameters)
        {
            Interlocked.Increment(ref _created);
            return new NullLogger();
        }

        private sealed class NullLogger : IExecutionLogger
        {
            public void Log(HandlerLogType level, string message, params object[] args)
            {
            }
        }
    }
}
