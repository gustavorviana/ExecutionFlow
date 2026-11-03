using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using ExecutionFlow.Hosting.Tests.Support;
using System.Diagnostics;

namespace ExecutionFlow.Hosting.Tests
{
    public class HostingOptionsTests
    {
        private static HostingSetup Configure(Action<HostingOptions> configure)
        {
            var setup = new HostingSetup();
            setup.Configure(configure);
            return setup;
        }

        // AC-006.2
        [Fact]
        public void MaxConcurrency_Throws_WhenLessThanOne()
        {
            var options = new HostingOptions();

            Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxConcurrency = 0);
            options.MaxConcurrency = 1;
            Assert.Equal(1, options.MaxConcurrency);
        }

        [Fact]
        public void MaxConcurrency_DefaultsToProcessorCount()
        {
            Assert.Equal(Environment.ProcessorCount, new HostingOptions().MaxConcurrency);
        }

        // AC-002.2
        [Fact]
        public void IsHandlerEnabled_PrefersGetPlanOf_OverGlobal()
        {
            var off = Configure(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(EveryMinuteB));
                o.GlobalRecurringAutoRun = true;
                o.GetPlanOf<EveryMinute>().Enabled(false);
            });
            Assert.False(off.IsEnabled(typeof(EveryMinute)));
            Assert.True(off.IsEnabled(typeof(EveryMinuteB)));

            var on = Configure(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(EveryMinuteB));
                o.GlobalRecurringAutoRun = false;
                o.GetPlanOf<EveryMinute>().Enabled(true);
            });
            Assert.True(on.IsEnabled(typeof(EveryMinute)));
            Assert.False(on.IsEnabled(typeof(EveryMinuteB)));
        }

        // AC-002.6
        [Fact]
        public async Task UserPlan_ReplacesGetPlanOf()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            var plan = planner.Build();

            var warnings = new RecordingTraceListener();
            Trace.Listeners.Add(warnings);
            try
            {
                await using var host = new TestHost(o =>
                {
                    o.UsePlan(plan);
                    o.GetPlanOf<EveryMinute>().Enabled(false);
                });

                Assert.True(host.Runtime.Setup.IsEnabled(typeof(EveryMinute)));
                Assert.Single(host.HostedServices.OfType<RecurringHandlerHost>());
                Assert.Contains(warnings.Messages, m => m.Contains(typeof(EveryMinute).FullName!) && m.Contains("GetPlanOf"));
            }
            finally
            {
                Trace.Listeners.Remove(warnings);
            }
        }

        // AC-003.11
        [Fact]
        public void PerHandlerSettings_WinOverGlobal()
        {
            // Outside the plan: GetPlanOf wins over the global option.
            var options = Configure(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(EveryMinuteB));
                o.IntervalFromEnd = true;
                o.SkipWhenOverlapping = true;
                o.GetPlanOf<EveryMinute>().IntervalFromEnd(false).SkipWhenOverlapping(false);
            }).Options;
            Assert.False(options.IntervalFromEndOf(typeof(EveryMinute)));
            Assert.False(options.SkipWhenOverlappingOf(typeof(EveryMinute)));
            Assert.True(options.IntervalFromEndOf(typeof(EveryMinuteB)));
            Assert.True(options.SkipWhenOverlappingOf(typeof(EveryMinuteB)));

            // In the plan: the step wins over the global option.
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>().IntervalFromEnd().SkipWhenOverlapping();
            var plan = planner.Build();
            var planOptions = Configure(o => o.UsePlan(plan)).Options;
            Assert.True(planOptions.IntervalFromEndOf(typeof(EveryMinute)));
            Assert.True(planOptions.SkipWhenOverlappingOf(typeof(EveryMinute)));
        }
    }
}
