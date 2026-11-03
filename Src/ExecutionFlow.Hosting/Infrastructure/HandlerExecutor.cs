using ExecutionFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>Runs one handler: a new DI scope, a <see cref="FlowContext"/> with a job ID and the execution loggers, then <c>HandleAsync</c>.</summary>
    internal sealed class HandlerExecutor
    {
        private readonly IServiceProvider _serviceProvider;

        public HandlerExecutor(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>The result of <see cref="ExecuteAsync"/>.</summary>
        public enum Status
        {
            /// <summary>The handler returned.</summary>
            Completed,

            /// <summary>The handler threw.</summary>
            Failed,

            /// <summary>The run was cancelled by <c>cancellationToken</c> (the host is stopping).</summary>
            Cancelled,
        }

        /// <summary>Runs the handler and returns how it ended, with the exception of a failed run.</summary>
        public async Task<(Status Status, Exception Error)> ExecuteAsync(Type handlerType, CancellationToken cancellationToken)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var handler = (IHandler)scope.ServiceProvider.GetRequiredService(handlerType);
                    var context = new FlowContextBuilder(scope.ServiceProvider.GetRequiredService<ExecutionLoggerFactory>())
                        .SetJob(Guid.NewGuid().ToString("N"), 1)
                        .SetHandler(handlerType)
                        .Build();

                    await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);
                    return (Status.Completed, null);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return (Status.Cancelled, null);
            }
            catch (Exception ex)
            {
                return (Status.Failed, ex);
            }
        }
    }
}
