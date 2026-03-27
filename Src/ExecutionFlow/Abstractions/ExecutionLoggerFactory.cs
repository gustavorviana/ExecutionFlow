using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Aggregates multiple <see cref="IExecutionLoggerFactory"/> instances and creates a composite logger.
    /// </summary>
    public class ExecutionLoggerFactory
    {
        private readonly IReadOnlyList<IExecutionLoggerFactory> _factories;

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionLoggerFactory"/>.
        /// </summary>
        /// <param name="factories">The logger factory implementations to aggregate.</param>
        public ExecutionLoggerFactory(IEnumerable<IExecutionLoggerFactory> factories)
        {
            if (factories == null) throw new System.ArgumentNullException(nameof(factories));
            _factories = factories.ToArray();
        }

        /// <summary>
        /// Creates a composite logger by invoking all registered factories and combining non-null results.
        /// </summary>
        /// <param name="jobParameters">The flow parameters for the current execution.</param>
        /// <returns>A composite <see cref="IExecutionLogger"/> that dispatches to all created loggers.</returns>
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
