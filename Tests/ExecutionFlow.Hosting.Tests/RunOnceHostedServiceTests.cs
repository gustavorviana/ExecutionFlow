using ExecutionFlow.Hosting.Infrastructure;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace ExecutionFlow.Hosting.Tests
{
    // Environment.ExitCode is process-wide: these tests don't run in parallel with each other.
    [Collection(nameof(RunOnceHostedServiceTests))]
    public class RunOnceHostedServiceTests
    {
        private static TestHost RunOnceHost(IHostApplicationLifetime lifetime, Action<HostingOptions> configure)
        {
            return new TestHost(o =>
            {
                o.RunOnce = true;
                configure(o);
            }, services => services.AddSingleton(lifetime));
        }

        // AC-007.5
        [Fact]
        public async Task StartAsync_RunsPlanAndStopsApplication()
        {
            var exitCode = Environment.ExitCode;
            var lifetime = Substitute.For<IHostApplicationLifetime>();
            await using var host = RunOnceHost(lifetime, o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(Hourly));
            });

            // No schedulers in run-once mode.
            var service = Assert.IsType<RunOnceHostedService>(Assert.Single(host.HostedServices));

            await service.StartAsync(CancellationToken.None);
            await service.Completion.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, host.Probe.Ended<EveryMinute>());
            Assert.Equal(1, host.Probe.Ended<Hourly>());
            lifetime.Received(1).StopApplication();
            Assert.Equal(exitCode, Environment.ExitCode);
        }

        // AC-007.6
        [Fact]
        public async Task StartAsync_SetsExitCode_WhenAHandlerFails()
        {
            var exitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                var lifetime = Substitute.For<IHostApplicationLifetime>();
                await using var host = RunOnceHost(lifetime, o =>
                {
                    o.Add(typeof(EveryMinute));
                    o.Add(typeof(EveryMinuteB));
                });
                host.Probe.Throw<EveryMinute>();

                var service = (RunOnceHostedService)host.HostedServices.Single();
                await service.StartAsync(CancellationToken.None);
                await service.Completion.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(1, Environment.ExitCode);
                Assert.Equal(1, host.Probe.Ended<EveryMinuteB>());
                lifetime.Received(1).StopApplication();
            }
            finally
            {
                Environment.ExitCode = exitCode;
            }
        }
    }
}
