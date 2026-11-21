using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>On its own schedule, but only after a new cycle of RecalculatePrices (the plan).</summary>
    [Recurring("*/15 * * * * *")]
    public sealed class PublishReportHandler : LoggedHandler
    {
        public PublishReportHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
