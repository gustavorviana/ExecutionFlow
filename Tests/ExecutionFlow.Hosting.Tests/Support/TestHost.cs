using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>A local host on a <see cref="FakeTimeProvider"/>, with a probe that records and controls every handler run.</summary>
    internal sealed class TestHost : IAsyncDisposable
    {
        public static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

        public FakeTimeProvider Time { get; } = new FakeTimeProvider(Start);
        public RunProbe Probe { get; } = new RunProbe();
        public ListLoggerProvider Logs { get; } = new ListLoggerProvider();
        public ServiceProvider Provider { get; }
        public IReadOnlyList<IHostedService> HostedServices { get; }

        private bool _started;

        public TestHost(Action<HostingOptions> configure, Action<IServiceCollection>? services = null)
        {
            var collection = new ServiceCollection();
            collection.AddSingleton(Probe);
            collection.AddLogging(b => b.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
            services?.Invoke(collection);
            collection.AddExecutionFlowHosting(o =>
            {
                o.TimeProvider = Time;
                configure(o);
            });

            Provider = collection.BuildServiceProvider();
            HostedServices = Provider.GetServices<IHostedService>().ToList();
        }

        public HostingRuntime Runtime => Provider.GetRequiredService<HostingRuntime>();

        public async Task StartAsync()
        {
            foreach (var service in HostedServices)
                await service.StartAsync(CancellationToken.None);

            _started = true;
            await Settle();
        }

        public async Task StopAsync()
        {
            if (!_started)
                return;

            _started = false;
            foreach (var service in HostedServices.Reverse())
                await service.StopAsync(CancellationToken.None);
        }

        /// <summary>Advances the fake clock one step at a time (default one second), letting the continuations of each step run.</summary>
        public async Task Advance(TimeSpan by, TimeSpan? step = null)
        {
            var size = step ?? TimeSpan.FromSeconds(1);
            for (var elapsed = TimeSpan.Zero; elapsed < by; elapsed += size)
            {
                Time.Advance(by - elapsed < size ? by - elapsed : size);
                await Settle();
            }
        }

        /// <summary>Gives the thread pool time to run the continuations released by the clock.</summary>
        public static async Task Settle()
        {
            for (var i = 0; i < 3; i++)
                await Task.Delay(5);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await Provider.DisposeAsync();
        }
    }
}
