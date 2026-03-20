using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Handlers.Recurring
{
    [Recurring("0 0 * * *")]
    public class RecurringCleanupHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
        {
            context.Log.Info("Cleanup finished.");
            return Task.CompletedTask;
        }
    }
}
