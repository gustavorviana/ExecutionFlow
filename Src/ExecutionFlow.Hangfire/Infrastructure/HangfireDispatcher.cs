using ExecutionFlow.Abstractions;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireDispatcher : IHangfireDispatcher
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly IJobIdGenerator _jobIdGenerator;
        private readonly IExecutionFlowRegistry _registry;
        private readonly JobStorage _jobStorage;
        private readonly RecurringJobManager _recurringJobManager;

        public HangfireDispatcher(IBackgroundJobClient jobClient, JobStorage jobStorage, IJobIdGenerator jobIdGenerator, IExecutionFlowRegistry registry)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));

            _jobIdGenerator = jobIdGenerator ?? throw new ArgumentNullException(nameof(jobIdGenerator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _recurringJobManager = new RecurringJobManager(jobStorage);
        }

        public string Publish<TEvent>(TEvent @event)
        {
            var customName = @event is ICustomNameEvent customNameEvent ? customNameEvent.CustomName : null;

            var jobId = _jobClient.Enqueue<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default));

            if (@event is ICustomIdEvent customIdEvent)
            {
                using (var connection = _jobStorage.GetConnection())
                {
                    connection.SetJobParameter(jobId, ContextConsts.CustomId, customIdEvent.CustomId);
                }
            }

            return jobId;
        }

        public void Trigger(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));

            if (!_registry.RecurringHandlers.ContainsKey(handlerType))
                throw new InvalidOperationException(
                    $"No recurring handler registered for type '{handlerType.FullName}'.");

            var jobId = _jobIdGenerator.GenerateId(handlerType);
            _recurringJobManager.Trigger(jobId);
        }

        public void Trigger(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException(nameof(jobId));

            _recurringJobManager.Trigger(jobId);
        }
    }
}
