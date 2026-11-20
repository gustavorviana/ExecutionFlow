using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Outside the plan, every second: the local host runs intervals from one second, to the second.</summary>
    [RecurringInterval("00:00:01")]
    public sealed class PulseHandler : LoggedHandler
    {
        public PulseHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
