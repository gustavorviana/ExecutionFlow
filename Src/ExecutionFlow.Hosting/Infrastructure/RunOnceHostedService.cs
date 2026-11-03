using ExecutionFlow.Hosting.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// The run-once mode (<see cref="HostingOptions.RunOnce"/>): runs every handler once in the background, sets the exit code
    /// to 1 when any failed, and stops the application.
    /// </summary>
    internal sealed class RunOnceHostedService : IHostedService
    {
        private readonly IExecutionPlanRunner _runner;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();

        public RunOnceHostedService(IExecutionPlanRunner runner, IHostApplicationLifetime lifetime, ILogger logger)
        {
            _runner = runner;
            _lifetime = lifetime;
            _logger = logger;
        }

        /// <summary>Gets the background run, available after <see cref="StartAsync"/>.</summary>
        internal Task Completion { get; private set; } = Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Completion = Task.Run(RunAsync);
            return Task.CompletedTask;
        }

        private async Task RunAsync()
        {
            try
            {
                var result = await _runner.RunOnceAsync(_stopping.Token).ConfigureAwait(false);
                foreach (var outcome in result.Outcomes)
                {
                    if (outcome.Status == HandlerRunStatus.Failed)
                        _logger.LogError(outcome.Exception, "ExecutionFlow: '{Handler}' failed.", outcome.HandlerType.FullName);
                    else
                        _logger.LogInformation("ExecutionFlow: {Outcome}", outcome);
                }

                if (result.HasFailures)
                    Environment.ExitCode = 1;
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                _logger.LogInformation("ExecutionFlow: the run-once was stopped before it finished.");
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopping.Cancel();
            await Task.WhenAny(Completion, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        }
    }
}
