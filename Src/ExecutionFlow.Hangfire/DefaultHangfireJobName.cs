using ExecutionFlow.Abstractions;
using Hangfire.Common;
using System;
using System.Linq;

namespace ExecutionFlow.Hangfire
{
    public class DefaultHangfireJobName : IHangfireJobName
    {
        private readonly IJobIdGenerator _idGenerator;
        private readonly IExecutionFlowRegistry _jobExecutionFlow;

        public DefaultHangfireJobName(IJobIdGenerator idGenerator, IExecutionFlowRegistry JobExecutionFlow)
        {
            _idGenerator = idGenerator;
            _jobExecutionFlow = JobExecutionFlow;
        }

        public string GetName(Job job)
        {
            var handlerInfo = GetEventInfo(job);
            if (handlerInfo != null)
                return string.IsNullOrEmpty(handlerInfo.DisplayName) ? handlerInfo.HandlerType.FullName : handlerInfo.DisplayName;

            if (job.Args.Count == 3 && job.Args[1] is Type jobType)
                return jobType.FullName;

            return $"{_idGenerator.GenerateId(job.Method.DeclaringType)}.{job.Method.Name}";
        }

        private HandlerRegistration GetEventInfo(Job job)
        {
            if (job.TryGetEventType(out var eventType))
                return _jobExecutionFlow.EventHandlers.TryGetValue(eventType, out var eventHandler) ? eventHandler : null;

            var handlerType = job.GetRecurringHandlerType();
            return handlerType == null ? null : _jobExecutionFlow.RecurringHandlers.FirstOrDefault(x => x.HandlerType.FullName == handlerType.FullName);
        }
    }
}
