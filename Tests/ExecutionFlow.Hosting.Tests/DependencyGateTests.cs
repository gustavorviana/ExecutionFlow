using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using ExecutionFlow.Hosting.Tests.Support;

namespace ExecutionFlow.Hosting.Tests
{
    public class DependencyGateTests
    {
        private static DependencyGate SinglePrerequisite()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            planner.Add<Dependent>().DependsOn<EveryMinute>();
            return new DependencyGate(planner.Build());
        }

        private static DependencyGate TwoPrerequisites()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            planner.Add<EveryMinuteB>();
            planner.Add<Dependent>().DependsOn<EveryMinute>().DependsOn<EveryMinuteB>();
            return new DependencyGate(planner.Build());
        }

        // AC-005.1
        [Fact]
        public void Complete_TriggersDependent_WhenAllPrerequisitesHaveNewCycles()
        {
            var gate = SinglePrerequisite();

            Assert.Equal(new[] { typeof(Dependent) }, gate.Complete(typeof(EveryMinute)));
        }

        // AC-005.2
        [Fact]
        public void Complete_DoesNotTrigger_WhenGenerationAlreadyConsumed()
        {
            var gate = SinglePrerequisite();
            gate.Complete(typeof(EveryMinute));

            // The dependent consumed the only cycle: when it ends, nothing new is there.
            Assert.False(gate.Finish(typeof(Dependent)));
            Assert.Empty(gate.Complete(typeof(Dependent)));

            // A new cycle triggers it again.
            Assert.Equal(new[] { typeof(Dependent) }, gate.Complete(typeof(EveryMinute)));
        }

        // AC-005.3
        [Fact]
        public void Complete_TriggersAfterLastPrerequisite()
        {
            var gate = TwoPrerequisites();

            Assert.Empty(gate.Complete(typeof(EveryMinute)));
            Assert.Equal(new[] { typeof(Dependent) }, gate.Complete(typeof(EveryMinuteB)));
        }

        // AC-005.8
        [Fact]
        public async Task Complete_TriggersOnce_WhenPrerequisitesCompleteConcurrently()
        {
            for (var i = 0; i < 200; i++)
            {
                var gate = TwoPrerequisites();
                using var start = new Barrier(2);

                var first = Task.Run(() => { start.SignalAndWait(); return gate.Complete(typeof(EveryMinute)).Count; });
                var second = Task.Run(() => { start.SignalAndWait(); return gate.Complete(typeof(EveryMinuteB)).Count; });

                Assert.Equal(1, await first + await second);
            }
        }

        [Fact]
        public void TryBegin_ReportsPendingPrerequisites_AndRunsOnceReady()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryTwoMinutes>();
            planner.Add<DependentOwn>().DependsOn<EveryTwoMinutes>().RunMode(DependentRunMode.OwnSchedule);
            var gate = new DependencyGate(planner.Build());

            Assert.False(gate.TryBegin(typeof(DependentOwn), out var reason));
            Assert.Contains(typeof(EveryTwoMinutes).FullName!, reason);

            // A dependent on its own schedule is never triggered by a completion; its next occurrence finds the cycle.
            Assert.Empty(gate.Complete(typeof(EveryTwoMinutes)));
            Assert.True(gate.TryBegin(typeof(DependentOwn), out _));
            Assert.False(gate.TryBegin(typeof(DependentOwn), out var running));
            Assert.Contains("still running", running);
        }
    }
}
