using System;
using ExecutionFlow.Abstractions;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireExecutionLogger : IExecutionLogger
    {
        private readonly PerformContext _performContext;

        public HangfireExecutionLogger(PerformContext performContext)
        {
            _performContext = performContext ?? throw new ArgumentNullException(nameof(performContext));
        }

        public void Info(object message)
        {
            WriteMessage("INFO", message);
        }

        public void Success(object message)
        {
            WriteMessage("SUCCESS", message);
        }

        public void Warning(object message)
        {
            WriteMessage("WARNING", message);
        }

        public void Error(object message)
        {
            WriteMessage("ERROR", message);
        }

        public void Error(Exception exception)
        {
            WriteMessage("ERROR", exception);
        }

        private void WriteMessage(string level, object message)
        {
            var jobId = _performContext.BackgroundJob?.Id;
            Console.WriteLine($"[{level}] [Job {jobId}] {message}");
        }
    }
}
