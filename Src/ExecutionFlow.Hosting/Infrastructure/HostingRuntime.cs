using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// Everything one local host uses at run time, created once per service provider: two hosts never share generations,
    /// overlap guards, the concurrency limit or schedulers (011 FR-010).
    /// </summary>
    internal sealed class HostingRuntime : IDisposable
    {
        internal const string LoggerCategory = "ExecutionFlow.Hosting";

        public HostingRuntime(HostingSetup setup, IServiceProvider serviceProvider)
        {
            Setup = setup;
            Clock = CreateClock(setup.Options);
            Logger = (serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger(LoggerCategory);
            Gate = new DependencyGate(setup.Plan);
            Limiter = new ConcurrencyLimiter(setup.Options.MaxConcurrency);
            OverlapGuard = new OverlapGuard(Clock);
            Executor = new HandlerExecutor(serviceProvider);
            Coordinator = new RunCoordinator(setup, Executor, Gate, Limiter, OverlapGuard, Clock, Logger);
            Runner = new ExecutionPlanRunner(setup, Executor);

            // Runs stop as soon as the application starts stopping, before the hosted services are stopped one by one.
            serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(Coordinator.Cancel);
        }

        public HostingSetup Setup { get; }
        public IClock Clock { get; }
        public ILogger Logger { get; }
        public DependencyGate Gate { get; }
        public ConcurrencyLimiter Limiter { get; }
        public OverlapGuard OverlapGuard { get; }
        public HandlerExecutor Executor { get; }
        public RunCoordinator Coordinator { get; }
        public ExecutionPlanRunner Runner { get; }

        private static IClock CreateClock(HostingOptions options)
        {
#if NET8_0_OR_GREATER
            return ReferenceEquals(options.TimeProvider, TimeProvider.System) ? (IClock)SystemClock.Instance : new TimeProviderClock(options.TimeProvider);
#else
            return SystemClock.Instance;
#endif
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            Limiter.Dispose();
        }
    }
}
