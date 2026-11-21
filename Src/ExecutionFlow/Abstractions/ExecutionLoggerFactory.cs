using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Aggregates multiple <see cref="IExecutionLoggerFactory"/> instances and creates a composite logger.
    /// A factory that fails to create its logger is skipped, so logging never prevents a job from running.
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
            if (factories == null) throw new ArgumentNullException(nameof(factories));
            _factories = factories.ToArray();
        }

        /// <summary>
        /// Creates a composite logger by invoking all registered factories and combining non-null results.
        /// </summary>
        /// <param name="jobParameters">The flow parameters for the current execution.</param>
        /// <returns>A composite <see cref="IExecutionLogger"/> that dispatches to all created loggers.</returns>
        public IExecutionLogger CreateLogger(FlowParameters jobParameters)
        {
            return CreateLogger(new ExecutionLoggerContext(jobParameters, null, 1, null, null));
        }

        /// <summary>
        /// Creates a composite logger for one execution. Factories implementing <see cref="IExecutionLoggerContextFactory"/>
        /// receive the whole <paramref name="context"/>; others receive its parameters.
        /// </summary>
        /// <param name="context">The execution's facts.</param>
        /// <returns>A composite <see cref="IExecutionLogger"/> that dispatches to all created loggers.</returns>
        public IExecutionLogger CreateLogger(ExecutionLoggerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var loggers = new List<IExecutionLogger>();
            foreach (var factory in _factories)
            {
                try
                {
                    var logger = factory is IExecutionLoggerContextFactory contextFactory
                        ? contextFactory.CreateLogger(context)
                        : factory.CreateLogger(context.Parameters);

                    if (logger != null)
                        loggers.Add(logger);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("ExecutionFlow: Logger factory '{0}' failed: {1}", factory.GetType().FullName, ex.Message);
                }
            }

            return new CompositeExecutionLogger(loggers);
        }
    }
}
