using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using Hangfire;
using System;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireDispatcher : IDispatcher
    {
        public string Enqueue<TEvent>(TEvent @event)
        {
            var registration = ExecutionFlowSetup.Registrations
                .FirstOrDefault(r => r.JobType == typeof(TEvent));

            if (registration == null)
                throw new InvalidOperationException(
                    $"No handler registered for event type '{typeof(TEvent).FullName}'.");

            var handlerTypeName = registration.HandlerType.AssemblyQualifiedName;

            var jobId = BackgroundJob.Enqueue<HangfireJobDispatcher>(
                x => x.DispatchEventAsync(@event, null, handlerTypeName, default));

            if (@event is ICustomIdEvent customIdEvent)
            {
                var customId = customIdEvent.GetCustomId();
                using (var connection = JobStorage.Current.GetConnection())
                {
                    connection.SetJobParameter(jobId, "customId", customId);
                }
            }

            return jobId;
        }
    }
}
