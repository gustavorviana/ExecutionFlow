using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>Base of the test handlers: every run goes through the probe.</summary>
    internal abstract class ProbeHandler : IHandler
    {
        private readonly RunProbe _probe;

        protected ProbeHandler(RunProbe probe)
        {
            _probe = probe;
        }

        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
        {
            return _probe.RunAsync(this, context, cancellationToken);
        }
    }
}
