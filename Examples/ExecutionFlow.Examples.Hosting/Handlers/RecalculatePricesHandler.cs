using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Triggered by FetchRates (the plan), at least 20 seconds after its previous run.</summary>
    public sealed class RecalculatePricesHandler : LoggedHandler
    {
        public RecalculatePricesHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}
