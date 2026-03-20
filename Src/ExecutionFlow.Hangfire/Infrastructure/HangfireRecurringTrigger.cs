using ExecutionFlow.Abstractions;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireRecurringTrigger : IRecurringTrigger
    {
        private readonly JobStorage _jobStorage;
        private readonly IJobIdGenerator _jobIdGenerator;
        private readonly IExecutionFlowRegistry _registry;

        public HangfireRecurringTrigger(JobStorage jobStorage, IJobIdGenerator jobIdGenerator, IExecutionFlowRegistry registry)
        {
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _jobIdGenerator = jobIdGenerator ?? throw new ArgumentNullException(nameof(jobIdGenerator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Trigger(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));

            if (!_registry.RecurringHandlers.ContainsKey(handlerType))
                throw new InvalidOperationException(
                    $"No recurring handler registered for type '{handlerType.FullName}'.");

            var jobId = _jobIdGenerator.GenerateId(handlerType);
            new RecurringJobManager(_jobStorage).Trigger(jobId);
        }

        public void Trigger(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException(nameof(jobId));

            new RecurringJobManager(_jobStorage).Trigger(jobId);
        }
    }
}
