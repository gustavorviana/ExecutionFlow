using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Handlers.Recurring;
using ExecutionFlow.Examples.Shared.Events;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Handlers.Events
{
    public class MessageProcessedEventHandler(IRecurringTrigger trigger) : IHandler<MessageProcessedEvent>
    {
        public Task HandleAsync(FlowContext<MessageProcessedEvent> context, CancellationToken cancellationToken)
        {
            context.Log.Info($"Message of {context.Event.From} processed");
            trigger.Trigger(typeof(RecurringCleanupHandler));
            trigger.Trigger(typeof(RecurringCleanupHandler).FullName);
            return Task.CompletedTask;
        }
    }
}