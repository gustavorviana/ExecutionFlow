using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.Logging;

namespace ExecutionFlow.Hosting.Tests
{
    /// <summary>015-hosting-second-intervals FR-007: turning off the log lines of skipped occurrences.</summary>
    public class RunCoordinatorTests
    {
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

        private static ExecutionPlan OwnSchedulePlan(Action<HandlerBuilder<DependentOwn>>? configure = null)
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryTwoMinutes>();
            var dependent = planner.Add<DependentOwn>().DependsOn<EveryTwoMinutes>().RunMode(DependentRunMode.OwnSchedule);
            configure?.Invoke(dependent);
            return planner.Build();
        }

        private static int SkipLines<T>(TestHost host) =>
            host.Logs.At(LogLevel.Information).Count(m => m.Contains("skipped") && m.Contains(typeof(T).FullName!));

        // AC-007.1
        [Fact]
        public async Task Skip_Logs_ByDefault()
        {
            await using (var host = new TestHost(o =>
            {
                o.Add(typeof(NoOverlapSecond));
                o.SkipWhenOverlapping = true;
            }))
            {
                host.Probe.On<NoOverlapSecond>(ct => Task.Delay(TimeSpan.FromSeconds(3), host.Time, ct));
                await host.StartAsync();
                await host.Advance(TimeSpan.FromSeconds(2));
                await Wait.Until(() => SkipLines<NoOverlapSecond>(host) >= 1, "the overlap skip was logged");
            }

            var plan = OwnSchedulePlan();
            await using (var host = new TestHost(o => o.UsePlan(plan)))
            {
                await host.StartAsync();
                await host.Advance(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                await Wait.Until(() => SkipLines<DependentOwn>(host) >= 1, "the prerequisite skip was logged");
            }
        }

        // AC-007.2
        [Fact]
        public async Task Skip_DoesNotLog_WhenTurnedOffGlobally()
        {
            await using (var host = new TestHost(o =>
            {
                o.Add(typeof(NoOverlapSecond));
                o.SkipWhenOverlapping = true;
                o.LogSkippedOccurrences = false;
            }))
            {
                host.Probe.On<NoOverlapSecond>(ct => Task.Delay(TimeSpan.FromSeconds(3), host.Time, ct));
                await host.StartAsync();
                await host.Advance(TimeSpan.FromSeconds(3));
                await TestHost.Settle();
                Assert.Equal(1, host.Probe.Started<NoOverlapSecond>());
                Assert.Equal(0, SkipLines<NoOverlapSecond>(host));
            }

            var plan = OwnSchedulePlan();
            await using (var host = new TestHost(o =>
            {
                o.UsePlan(plan);
                o.LogSkippedOccurrences = false;
            }))
            {
                await host.StartAsync();
                await host.Advance(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                await TestHost.Settle();
                Assert.Equal(0, host.Probe.Started<DependentOwn>());
                Assert.Equal(0, SkipLines<DependentOwn>(host));
            }
        }

        // AC-007.2
        [Fact]
        public async Task Failure_StillLogs_WhenSkipLogsAreOff()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EverySecond));
                o.LogSkippedOccurrences = false;
            });
            host.Probe.Throw<EverySecond>();
            await host.StartAsync();

            await host.Advance(Second);
            await Wait.Until(() => host.Logs.At(LogLevel.Error).Any(m => m.Contains(typeof(EverySecond).FullName!)),
                "the failed run was logged as an error");
        }
    }
}
