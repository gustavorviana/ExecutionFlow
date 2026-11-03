using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Outside the plan, turned off with GetPlanOf: registered but never scheduled.</summary>
    [RecurringInterval("00:01:00")]
    public sealed class CleanupHandler : LoggedHandler
    {
        public CleanupHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
