using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Examples.Handlers.Recurring
{
    [DisplayName("Auto Run Disabled")]
    [Recurring("* * * * *")]
    public class AutoRunDisabledHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
        {
            context.Log.Warning("Running");
            return Task.CompletedTask;
        }
    }
}
