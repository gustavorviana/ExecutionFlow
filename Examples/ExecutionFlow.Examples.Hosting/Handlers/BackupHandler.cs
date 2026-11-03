using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Takes 8 seconds every 5 seconds: overlap is off, and the plan skips an occurrence while a run is in progress.</summary>
    [Recurring("*/5 * * * * *")]
    [NoOverlap(30)]
    public sealed class BackupHandler : LoggedHandler
    {
        public BackupHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
    }
}
