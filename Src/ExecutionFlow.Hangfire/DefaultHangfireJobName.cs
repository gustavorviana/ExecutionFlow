using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;
using System;

namespace ExecutionFlow.Hangfire
{
    public class DefaultHangfireJobName : IHangfireJobName
    {
        private readonly IJobIdGenerator _idGenerator;
        private readonly IExecutionFlowRegistry _jobExecutionFlow;

        public DefaultHangfireJobName(IJobIdGenerator idGenerator, IExecutionFlowRegistry jobExecutionFlow)
        {
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            _jobExecutionFlow = jobExecutionFlow ?? throw new ArgumentNullException(nameof(jobExecutionFlow));
        }

        public string GetName(Job job)
        {
            var handlerInfo = HangfireJobInfo.Create(job)?.GetExpectedName(_jobExecutionFlow);
            if (handlerInfo != null)
                return handlerInfo;

            return _idGenerator.GenerateId(job.Method.DeclaringType);
        }
    }
}
