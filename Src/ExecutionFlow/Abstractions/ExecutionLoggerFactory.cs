using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    public class ExecutionLoggerFactory
    {
        private readonly IReadOnlyList<IExecutionLoggerFactory> _factories;

        public ExecutionLoggerFactory(IEnumerable<IExecutionLoggerFactory> factories)
        {
            if (factories == null) throw new System.ArgumentNullException(nameof(factories));
            _factories = factories.ToArray();
        }

        public IExecutionLogger CreateLogger(FlowParameters jobParameters)
        {
            var loggers = _factories
                .Select(f => f.CreateLogger(jobParameters))
                .Where(l => l != null)
                .ToList();

            return new CompositeExecutionLogger(loggers);
        }
    }
}