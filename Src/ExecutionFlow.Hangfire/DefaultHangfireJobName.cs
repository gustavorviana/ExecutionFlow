using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;

namespace ExecutionFlow.Hangfire
{
    public class DefaultHangfireJobName : IHangfireJobName
    {
        private readonly IJobIdGenerator _idGenerator;
        private readonly IExecutionFlowRegistry _jobExecutionFlow;

        public DefaultHangfireJobName(IJobIdGenerator idGenerator, IExecutionFlowRegistry jobExecutionFlow)
        {
            _idGenerator = idGenerator;
            _jobExecutionFlow = jobExecutionFlow;
        }

        public string GetName(Job job)
        {
            var handlerInfo = HangfireJobInfo.Create(job)?.GetExpectedName(_jobExecutionFlow);
            return handlerInfo ?? HangfireRecurringJobInfo.GetJobType(job)?.FullName ?? $"{_idGenerator.GenerateId(job.Method.DeclaringType)}.{job.Method.Name}";
        }
    }
}
