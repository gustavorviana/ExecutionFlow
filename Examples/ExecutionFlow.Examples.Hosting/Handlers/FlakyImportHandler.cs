using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Fails every other run: a failing handler never stops the others.</summary>
    [Recurring("*/20 * * * * *")]
    public sealed class FlakyImportHandler : LoggedHandler
    {
        private static int _runs;

        public FlakyImportHandler(RunLog log) : base(log) { }

        protected override Task RunAsync(FlowContext context, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _runs) % 2 == 1)
                throw new InvalidOperationException("the import source is unavailable");

            return Task.CompletedTask;
        }
    }
}
