using ExecutionFlow.Abstractions;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireDispatcher : IEventDispatcher
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
            var customName = @event is ICustomNameEvent customNameEvent ? customNameEvent.CustomName : null;

            var jobId = _jobClient.Enqueue<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default));

            if (@event is ICustomIdEvent customIdEvent)
            {
                using (var connection = _jobStorage.GetConnection())
                {
                    connection.SetJobParameter(jobId, EventId, customIdEvent.CustomId);
                }
            }

            return jobId;
        }
    }
}
