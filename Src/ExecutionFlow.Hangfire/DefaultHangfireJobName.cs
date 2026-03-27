using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;
using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Default implementation of <see cref="IHangfireJobName"/> that resolves job names from the handler registry.
    /// Falls back to the ID generator if no registered handler is found.
    /// </summary>
    public class DefaultHangfireJobName : IHangfireJobName
    {
        private readonly IJobIdGenerator _idGenerator;
        private readonly IExecutionFlowRegistry _jobExecutionFlow;

        /// <summary>
        /// Initializes a new instance of <see cref="DefaultHangfireJobName"/>.
        /// </summary>
        /// <param name="idGenerator">The ID generator for fallback naming.</param>
        /// <param name="jobExecutionFlow">The handler registry to look up display names.</param>
        public DefaultHangfireJobName(IJobIdGenerator idGenerator, IExecutionFlowRegistry jobExecutionFlow)
        {
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            _jobExecutionFlow = jobExecutionFlow ?? throw new ArgumentNullException(nameof(jobExecutionFlow));
        }

        /// <inheritdoc />
        public string GetName(Job job)
        {
            var handlerInfo = HangfireJobInfo.Create(job)?.GetExpectedName(_jobExecutionFlow);
            if (handlerInfo != null)
                return handlerInfo;

            return _idGenerator.GenerateId(job.Method.DeclaringType);
        }
    }
}
