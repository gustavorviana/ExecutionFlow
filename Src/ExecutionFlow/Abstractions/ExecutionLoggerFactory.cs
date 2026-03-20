using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    public class ExecutionLoggerFactory
    {
        private readonly IReadOnlyList<IExecutionLoggerFactory> _factories;

        public ExecutionLoggerFactory(IEnumerable<IExecutionLoggerFactory> factories)
        {
            _factories = factories.ToArray();
        }

        public IExecutionLogger CreateLogger(IDictionary<string, object> jobParameters)
        {
            var loggers = _factories
                .Select(f => f.CreateLogger(jobParameters))
                .Where(l => l != null)
                .ToList();

            return new CompositeExecutionLogger(loggers);
        }
    }
}