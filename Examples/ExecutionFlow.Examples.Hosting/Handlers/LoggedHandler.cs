using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Hosting.Handlers
{
    /// <summary>Records every run, so the example's API and page can show what the local host did.</summary>
    public abstract class LoggedHandler : IHandler
    {
        private readonly RunLog _log;

        protected LoggedHandler(RunLog log)
        {
            _log = log;
        }

        public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
        {
            var run = _log.Start(GetType().Name, context.JobId);
            try
            {
                await RunAsync(context, cancellationToken);
                _log.End(run, "Succeeded");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log.End(run, "Cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _log.End(run, "Failed: " + ex.Message);
                throw;
            }
        }

        protected abstract Task RunAsync(FlowContext context, CancellationToken cancellationToken);
    }
}
