using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Tests.Support;

namespace ExecutionFlow.Hosting.Tests
{
    public class HostIsolationTests
    {
        // AC-010.1
        [Fact]
        public async Task TwoHosts_DoNotShareGenerations()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            planner.Add<Dependent>().DependsOn<EveryMinute>();
            var plan = planner.Build();

            await using var first = new TestHost(o => o.UsePlan(plan));
            await using var second = new TestHost(o => o.UsePlan(plan));
            await first.StartAsync();
            await second.StartAsync();

            // Only the first host's clock moves: its prerequisite completes and triggers its own dependent.
            await first.Advance(TimeSpan.FromMinutes(1));
            await Wait.Until(() => first.Probe.Ended<Dependent>() == 1, "the dependent ran in the first host");

            Assert.Equal(0, second.Probe.Started<Dependent>());
            Assert.Empty(second.Runtime.Gate.Complete(typeof(Dependent)));
            Assert.NotSame(first.Runtime.Gate, second.Runtime.Gate);
        }

        // AC-010.2
        [Fact]
        public async Task TwoHosts_DoNotShareOverlapGuards()
        {
            await using var first = new TestHost(o => o.Add(typeof(NoOverlapMinute)));
            await using var second = new TestHost(o => o.Add(typeof(NoOverlapMinute)));
            var releaseFirst = first.Probe.Hold<NoOverlapMinute>();
            var releaseSecond = second.Probe.Hold<NoOverlapMinute>();
            await first.StartAsync();
            await second.StartAsync();

            // Both run the same no-overlap handler at the same time: neither waits for the other.
            await Task.WhenAll(first.Advance(TimeSpan.FromMinutes(1)), second.Advance(TimeSpan.FromMinutes(1)));
            await Wait.Until(() => first.Probe.Started<NoOverlapMinute>() == 1 && second.Probe.Started<NoOverlapMinute>() == 1,
                "both hosts are running the handler");

            releaseFirst.SetResult();
            releaseSecond.SetResult();
        }
    }
}
