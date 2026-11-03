using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExecutionFlow.Hosting.Tests
{
    public class RecurringHandlerHostTests
    {
        private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

        private static ExecutionPlan Plan(Action<ExecutionPlanner> declare)
        {
            var planner = new ExecutionPlanner();
            declare(planner);
            return planner.Build();
        }

        // AC-003.1
        [Fact]
        public async Task Execute_RunsIntervalHandler_WhenIntervalElapses()
        {
            await using var host = new TestHost(o => o.Add(typeof(EveryMinute)));
            await host.StartAsync();

            await host.Advance(TimeSpan.FromSeconds(59));
            Assert.Equal(0, host.Probe.Started<EveryMinute>());

            await host.Advance(TimeSpan.FromSeconds(1));
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 1, "the interval handler ran");
        }

        // AC-003.2
        [Fact]
        public async Task Execute_RunsCronHandler_AtNextOccurrence()
        {
            await using var host = new TestHost(o => o.Add(typeof(Hourly)));
            await host.StartAsync();

            // Started at 12:00 exactly: the first run is the next occurrence, 13:00, never at startup.
            await host.Advance(TimeSpan.FromMinutes(59), Minute);
            Assert.Equal(0, host.Probe.Started<Hourly>());

            await host.Advance(Minute, Minute);
            await Wait.Until(() => host.Probe.Started<Hourly>() == 1, "the cron handler ran at 13:00");
        }

        // AC-003.3
        [Fact]
        public async Task Execute_PassesFlowContext_WithJobIdAndLoggers()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EveryMinute));
                o.AddLogger<RecordingLoggerFactory>();
            });
            await host.StartAsync();

            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Ended<EveryMinute>() == 2, "two runs ended");

            var contexts = host.Probe.Contexts.ToList();
            Assert.All(contexts, c => Assert.False(string.IsNullOrEmpty(c.JobId)));
            Assert.All(contexts, c => Assert.Equal(1, c.AttemptNumber));
            Assert.Equal(2, contexts.Select(c => c.JobId).Distinct().Count());

            // A new scope per run: the scoped handler is a new instance each time.
            Assert.Equal(2, host.Probe.Instances.Distinct().Count());

            var loggers = host.Provider.GetServices<IExecutionLoggerFactory>().OfType<RecordingLoggerFactory>().Single();
            Assert.Equal(2, loggers.Created);
        }

        // AC-003.4
        [Fact]
        public async Task Execute_EvaluatesCron_InResolvedTimeZone()
        {
            // [Recurring(TimeZone = "America/Sao_Paulo")]: 10:00 there is 13:00 UTC, one hour after the 12:00 UTC start.
            await using var attribute = new TestHost(o => o.Add(typeof(TenAmSaoPaulo)));
            await attribute.StartAsync();
            await attribute.Advance(TimeSpan.FromMinutes(59), Minute);
            Assert.Equal(0, attribute.Probe.Started<TenAmSaoPaulo>());
            await attribute.Advance(Minute, Minute);
            await Wait.Until(() => attribute.Probe.Started<TenAmSaoPaulo>() == 1, "the cron ran at 10:00 in São Paulo");

            // GetPlanOf<T>().TimeZone wins over the attribute: 10:00 UTC is 22 hours away, so nothing runs at 13:00 UTC.
            await using var configured = new TestHost(o =>
            {
                o.Add(typeof(TenAmSaoPaulo));
                o.GetPlanOf<TenAmSaoPaulo>().TimeZone("UTC");
            });
            await configured.StartAsync();
            await configured.Advance(TimeSpan.FromMinutes(61), Minute);
            Assert.Equal(0, configured.Probe.Started<TenAmSaoPaulo>());

            // The global RecurringTimeZone applies to a cron without one.
            await using var global = new TestHost(o =>
            {
                o.Add(typeof(Hourly));
                o.RecurringTimeZone = "America/Sao_Paulo";
            });
            Assert.Equal("America/Sao_Paulo", global.Runtime.Setup.TimeZoneOf(global.Runtime.Setup.RecurringHandlers[typeof(Hourly)]).Id);
        }

        // AC-003.5
        [Fact]
        public async Task Execute_StopsAndWarns_WhenCronHasNoNextOccurrence()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(NeverAgain));
                o.Add(typeof(EveryMinute));
            });
            await host.StartAsync();

            await Wait.Until(() => host.Logs.At(LogLevel.Warning).Any(m => m.Contains(typeof(NeverAgain).FullName!)), "a warning names the handler");

            // Only that handler stops.
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 1, "the other handler keeps running");
        }

        // AC-003.6
        [Fact]
        public async Task Execute_KeepsFixedCadence_ForIntervalByDefault()
        {
            await using var host = new TestHost(o => o.Add(typeof(EveryMinute)));
            var release = host.Probe.Hold<EveryMinute>();
            await host.StartAsync();

            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 1, "the first run started");

            // The first run is still running: the next one starts on the cadence and overlaps (allowed by default).
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 2, "the second run started on the cadence");
            Assert.Equal(2, host.Probe.MaxRunning<EveryMinute>());

            release.SetResult();
        }

        // AC-003.7
        [Fact]
        public async Task Execute_CountsFromEnd_WhenConfigured()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EveryMinute));
                o.IntervalFromEnd = true;
            });
            host.Probe.On<EveryMinute>(ct => Task.Delay(TimeSpan.FromSeconds(30), host.Time, ct));
            await host.StartAsync();

            // Runs at 12:01 and ends at 12:01:30, so the next run is at 12:02:30.
            await host.Advance(Minute);
            await host.Advance(TimeSpan.FromSeconds(30));
            await Wait.Until(() => host.Probe.Ended<EveryMinute>() == 1, "the first run ended");

            await host.Advance(TimeSpan.FromSeconds(59));
            Assert.Equal(1, host.Probe.Started<EveryMinute>());

            await host.Advance(TimeSpan.FromSeconds(1));
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 2, "the next run is an interval after the end");
        }

        // AC-003.8
        [Fact]
        public async Task Execute_WaitsForRunningRun_WhenOverlapOff()
        {
            await using var host = new TestHost(o => o.Add(typeof(NoOverlapMinute)));
            var release = host.Probe.Hold<NoOverlapMinute>();
            await host.StartAsync();

            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<NoOverlapMinute>() == 1, "the first run started");

            await host.Advance(Minute);
            await TestHost.Settle();
            Assert.Equal(1, host.Probe.Started<NoOverlapMinute>());

            release.SetResult();
            await Wait.Until(() => host.Probe.Started<NoOverlapMinute>() == 2, "the waiting run started when the first ended");
            Assert.Equal(1, host.Probe.MaxRunning<NoOverlapMinute>());
        }

        // AC-003.9
        [Fact]
        public async Task Execute_FailsWaitingRun_WhenOverlapTimeoutExpires()
        {
            await using var host = new TestHost(o => o.Add(typeof(NoOverlapMinute)));
            var release = host.Probe.Hold<NoOverlapMinute>();
            await host.StartAsync();

            // 12:01 runs and holds; 12:02 waits up to [NoOverlap(90)], so it fails at 12:03:30.
            await host.Advance(Minute);
            await host.Advance(Minute);
            await host.Advance(TimeSpan.FromSeconds(89));
            Assert.DoesNotContain(host.Logs.At(LogLevel.Error), m => m.Contains("waited more than"));

            await host.Advance(TimeSpan.FromSeconds(1));
            await Wait.Until(() => host.Logs.At(LogLevel.Error).Any(m => m.Contains(typeof(NoOverlapMinute).FullName!) && m.Contains("waited more than")),
                "the waiting run failed with an error log");
            Assert.Equal(1, host.Probe.Started<NoOverlapMinute>());

            // The scheduler continues.
            release.SetResult();
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<NoOverlapMinute>() >= 2, "later occurrences still run");
        }

        // AC-003.10
        [Fact]
        public async Task Execute_SkipsOccurrence_WhenOverlapOffAndSkipConfigured()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(NoOverlapMinute));
                o.SkipWhenOverlapping = true;
            });
            var release = host.Probe.Hold<NoOverlapMinute>();
            await host.StartAsync();

            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<NoOverlapMinute>() == 1, "the first run started");

            await host.Advance(Minute);
            await Wait.Until(() => host.Logs.At(LogLevel.Information).Any(m => m.Contains("skipped") && m.Contains(typeof(NoOverlapMinute).FullName!)),
                "the occurrence was skipped");

            // Releasing it doesn't run the skipped occurrence; the next one runs.
            release.SetResult();
            await TestHost.Settle();
            Assert.Equal(1, host.Probe.Started<NoOverlapMinute>());
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<NoOverlapMinute>() == 2, "the next occurrence ran");
        }

        // AC-004.1
        [Fact]
        public async Task Execute_KeepsRunning_WhenHandlerThrows()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(EveryMinuteB));
            });
            host.Probe.Throw<EveryMinute>();
            await host.StartAsync();

            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 2 && host.Probe.Ended<EveryMinuteB>() == 2, "both handlers kept running");
            await Wait.Until(() => host.Logs.At(LogLevel.Error).Count(m => m.Contains(typeof(EveryMinute).FullName!)) == 2, "each failure was logged");
        }

        // AC-004.2
        [Fact]
        public async Task Execute_DoesNotCompleteCycle_WhenHandlerThrows()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            host.Probe.Throw<EveryMinute>();
            await host.StartAsync();

            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 2, "the prerequisite ran twice");
            await TestHost.Settle();
            Assert.Equal(0, host.Probe.Started<Dependent>());
        }

        // AC-004.3
        [Fact]
        public async Task Execute_EndsQuietly_WhenHostStopsDuringRun()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            host.Probe.Hold<EveryMinute>();
            await host.StartAsync();

            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 1, "the run started");

            await host.StopAsync();

            Assert.Equal(0, host.Probe.Ended<EveryMinute>());
            Assert.Equal(0, host.Probe.Started<Dependent>());
            Assert.Empty(host.Logs.At(LogLevel.Error));
            await Wait.Until(() => host.Runtime.Coordinator.InFlightCount == 0, "every run and trigger ended");
        }

        // AC-009.1
        [Fact]
        public async Task Execute_UsesConfiguredTimeProvider()
        {
            await using var host = new TestHost(o => o.Add(typeof(EveryMinute)));

            Assert.IsType<TimeProviderClock>(host.Runtime.Clock);
            Assert.Equal(host.Time.GetUtcNow(), host.Runtime.Clock.UtcNow);

            // Only the fake clock moves the schedule: real time passing runs nothing.
            await host.StartAsync();
            await Task.Delay(200);
            Assert.Equal(0, host.Probe.Started<EveryMinute>());
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<EveryMinute>() == 1, "the fake clock drove the run");
        }

        // AC-005.4
        [Fact]
        public async Task Dependent_PostponesTrigger_WhenMinIntervalNotElapsed()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>().RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(5));
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            await host.StartAsync();

            // 12:01: the first trigger runs at once.
            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Ended<Dependent>() == 1, "the first trigger ran");

            // 12:02: triggered again, but the minimum interval ends at 12:06, so it's postponed, not lost.
            await host.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
            Assert.Equal(1, host.Probe.Started<Dependent>());
            await Wait.Until(() => host.Logs.At(LogLevel.Information).Any(m => m.Contains("minimum interval")), "the postponement was logged");

            await host.Advance(TimeSpan.FromSeconds(1));
            await Wait.Until(() => host.Probe.Started<Dependent>() == 2, "the postponed trigger ran at the end of the interval");
        }

        // AC-005.5
        [Fact]
        public async Task Dependent_SkipsOccurrence_WhenPrerequisitesNotMet()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryTwoMinutes>();
                p.Add<DependentOwn>().DependsOn<EveryTwoMinutes>().RunMode(DependentRunMode.OwnSchedule);
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            await host.StartAsync();

            // 12:01: its own occurrence, but EveryTwoMinutes hasn't completed a cycle yet.
            await host.Advance(Minute);
            await Wait.Until(() => host.Logs.At(LogLevel.Information).Any(m =>
                m.Contains("skipped") && m.Contains(typeof(DependentOwn).FullName!) && m.Contains(typeof(EveryTwoMinutes).FullName!)),
                "the occurrence was skipped naming the pending prerequisite");
            Assert.Equal(0, host.Probe.Started<DependentOwn>());

            // After a cycle (12:02), a later occurrence runs; it isn't triggered by the completion itself.
            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Started<DependentOwn>() == 1, "an occurrence after a new cycle ran");
        }

        // AC-005.6
        [Fact]
        public async Task Execute_RunsDependent_OnlyAfterNewPrerequisiteCycle()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            await host.StartAsync();

            await host.Advance(TimeSpan.FromMinutes(3));
            await Wait.Until(() => host.Probe.Ended<Dependent>() == 3, "the dependent ran after each cycle");

            // Before each run of the dependent, its prerequisite completed one more cycle.
            var events = host.Probe.Events;
            var runs = 0;
            var cycles = 0;
            foreach (var e in events)
            {
                if (e == "end:" + nameof(EveryMinute))
                    cycles++;
                else if (e == "start:" + nameof(Dependent))
                    Assert.True(cycles > runs++, "the dependent ran without a new cycle");
            }
        }

        // AC-005.7
        [Fact]
        public async Task Dependent_RechecksAfterRun_InsteadOfOverlapping()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>();
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            var release = host.Probe.Hold<Dependent>();
            await host.StartAsync();

            await host.Advance(Minute);
            await Wait.Until(() => host.Probe.Started<Dependent>() == 1, "the dependent started");

            // Two more cycles while it runs: it isn't triggered again.
            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Ended<EveryMinute>() == 3, "the prerequisite completed two more cycles");
            Assert.Equal(1, host.Probe.Started<Dependent>());

            // When it ends it re-checks and runs once for the cycles it missed.
            release.SetResult();
            await Wait.Until(() => host.Probe.Ended<Dependent>() == 2, "it ran again after finishing");
            await TestHost.Settle();
            Assert.Equal(2, host.Probe.Started<Dependent>());
            Assert.Equal(1, host.Probe.MaxRunning<Dependent>());
        }

        // AC-005.9
        [Fact]
        public async Task Dependent_CancelsPendingTrigger_WhenHostStops()
        {
            var plan = Plan(p =>
            {
                p.Add<EveryMinute>();
                p.Add<Dependent>().DependsOn<EveryMinute>().RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(30));
            });
            await using var host = new TestHost(o => o.UsePlan(plan));
            await host.StartAsync();

            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Logs.At(LogLevel.Information).Any(m => m.Contains("minimum interval")), "a trigger is postponed");
            Assert.True(host.Runtime.Coordinator.InFlightCount > 0);

            await host.StopAsync();

            await Wait.Until(() => host.Runtime.Coordinator.InFlightCount == 0, "every run and trigger ended");
            Assert.Equal(1, host.Probe.Started<Dependent>());
            Assert.Empty(host.Logs.At(LogLevel.Error));
        }

        // SC-001
        [Fact]
        public async Task WaitingRuns_HoldNoThreads()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(NoOverlapMinute));
                o.MaxConcurrency = 1;
            });
            var releaseLimited = host.Probe.Hold<EveryMinute>();
            var releaseGuarded = host.Probe.Hold<NoOverlapMinute>();
            var coordinator = host.Runtime.Coordinator;

            var before = ThreadPool.ThreadCount;

            // 100 runs waiting for the single concurrency slot, and 100 waiting for an overlapping run (behind the slot too).
            for (var i = 0; i < 100; i++)
            {
                coordinator.StartOccurrence(typeof(EveryMinute));
                coordinator.StartOccurrence(typeof(NoOverlapMinute));
            }

            await Task.Delay(300);
            Assert.Equal(1, host.Probe.Started<EveryMinute>() + host.Probe.Started<NoOverlapMinute>());
            Assert.True(ThreadPool.ThreadCount - before <= 4, $"the thread pool grew from {before} to {ThreadPool.ThreadCount}");

            releaseLimited.SetResult();
            releaseGuarded.SetResult();
            await Wait.Until(() => coordinator.InFlightCount == 0, "every waiting run ended");
        }
    }
}
