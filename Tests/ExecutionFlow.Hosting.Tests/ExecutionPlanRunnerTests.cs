using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Abstractions;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ExecutionFlow.Hosting.Tests
{
    public class ExecutionPlanRunnerTests
    {
        private static ExecutionPlan Plan(Action<ExecutionPlanner> declare)
        {
            var planner = new ExecutionPlanner();
            declare(planner);
            return planner.Build(MissingPrerequisite.SkipDependents);
        }

        private static Task<RunOnceResult> RunOnce(TestHost host)
        {
            return host.Provider.GetRequiredService<IExecutionPlanRunner>().RunOnceAsync(CancellationToken.None);
        }

        private static string[] Started(TestHost host)
        {
            return host.Probe.Events.Where(e => e.StartsWith("start:")).Select(e => e.Substring(6)).ToArray();
        }

        // AC-007.1
        [Fact]
        public async Task RunOnceAsync_RunsHandlers_InStepOrder()
        {
            var plan = Plan(p =>
            {
                p.Add<Dependent>().DependsOn<EveryMinute>();
                p.Add<EveryMinuteB>();
                p.Add<EveryMinute>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));

            var result = await RunOnce(host);

            var order = plan.Steps.Select(s => s.HandlerType).ToArray();
            Assert.Equal(order, result.Outcomes.Select(o => o.HandlerType));
            Assert.Equal(order.Select(t => t.Name), Started(host));
            Assert.All(result.Outcomes, o => Assert.Equal(HandlerRunStatus.Completed, o.Status));
            Assert.False(result.HasFailures);
        }

        // AC-007.2
        [Fact]
        public async Task RunOnceAsync_SkipsDependents_WhenPrerequisiteFails()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
                p.Add<Dependent2>().DependsOn<Dependent>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            host.Probe.Throw<EveryMinute>();

            var result = await RunOnce(host);

            Assert.Equal(HandlerRunStatus.Failed, result.Outcomes[0].Status);
            Assert.Equal(HandlerRunStatus.Skipped, result.Outcomes[1].Status);
            Assert.Contains(nameof(EveryMinute), result.Outcomes[1].Reason);

            // A skipped prerequisite skips its dependents too.
            Assert.Equal(HandlerRunStatus.Skipped, result.Outcomes[2].Status);
            Assert.Contains(nameof(Dependent), result.Outcomes[2].Reason);
            Assert.True(result.HasFailures);
        }

        // AC-007.3
        [Fact]
        public async Task RunOnceAsync_SkipsDisabledHandlers()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<EveryMinuteB>().Enabled(false);
            });
            await using var host = new TestHost(o => o.UsePlan(plan));

            var result = await RunOnce(host);

            Assert.Equal(HandlerRunStatus.Skipped, result.Outcomes.Single(o => o.HandlerType == typeof(EveryMinuteB)).Status);
            Assert.Equal(new[] { nameof(EveryMinute) }, Started(host));
        }

        // AC-007.4
        [Fact]
        public async Task RunOnceAsync_ContinuesWithIndependentHandlers_WhenOneFails()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<EveryMinuteB>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            host.Probe.Throw<EveryMinute>();

            var result = await RunOnce(host);

            var failed = result.Outcomes[0];
            Assert.Equal(HandlerRunStatus.Failed, failed.Status);
            Assert.IsType<InvalidOperationException>(failed.Exception);
            Assert.Equal(HandlerRunStatus.Completed, result.Outcomes[1].Status);
        }

        // AC-007.7
        [Fact]
        public async Task RunOnceAsync_RunsHandlersOutsidePlan_AfterPlanInRegistrationOrder()
        {
            var plan = Plan(p => p.Add<EveryMinute>());
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(Hourly));
                o.Add(typeof(EveryTwoMinutes));
                o.Add(typeof(EveryMinuteB));
                o.UsePlan(plan);
                o.GetPlanOf<EveryTwoMinutes>().Enabled(false);
            });

            var result = await RunOnce(host);

            Assert.Equal(new[] { typeof(EveryMinute), typeof(Hourly), typeof(EveryTwoMinutes), typeof(EveryMinuteB) },
                result.Outcomes.Select(o => o.HandlerType));
            var disabled = result.Outcomes[2];
            Assert.Equal(HandlerRunStatus.Skipped, disabled.Status);
            Assert.Equal("disabled", disabled.Reason);
            Assert.Equal(new[] { nameof(EveryMinute), nameof(Hourly), nameof(EveryMinuteB) }, Started(host));
        }

        // AC-007.8
        [Fact]
        public async Task RunOnceAsync_RunsPlanHandlerOnce_WhenAlsoRegistered()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
            });
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(Dependent));
                o.Add(typeof(EveryMinute));
                o.UsePlan(plan);
            });

            var result = await RunOnce(host);

            Assert.Equal(new[] { typeof(EveryMinute), typeof(Dependent) }, result.Outcomes.Select(o => o.HandlerType));
            Assert.Equal(new[] { nameof(EveryMinute), nameof(Dependent) }, Started(host));
        }
    }
}
