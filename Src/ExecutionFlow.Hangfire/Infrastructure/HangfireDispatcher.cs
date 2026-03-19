using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireDispatcher : IDispatcher
    {
        internal const string EventId = "customId";
        private readonly IBackgroundJobClient _jobClient;
        private readonly JobStorage _jobStorage;

        public HangfireDispatcher(IBackgroundJobClient jobClient, JobStorage jobStorage)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        }

        public string Publish<TEvent>(TEvent @event)
        {
            var jobId = _jobClient.Enqueue<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, null, default));

            if (@event is ICustomIdEvent customIdEvent)
            {
                var customId = customIdEvent.GetCustomId();
                using (var connection = _jobStorage.GetConnection())
                {
                    connection.SetJobParameter(jobId, EventId, customId);
                }
            }

            return jobId;
        }
    }
}
