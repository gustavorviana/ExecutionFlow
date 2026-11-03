using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>A root of the plan: every 10 seconds (a cron with seconds).</summary>
    [Recurring("*/10 * * * * *")]
    public sealed class FetchRatesHandler : LoggedHandler
    {
        public FetchRatesHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}
