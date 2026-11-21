using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.Logging;

namespace ExecutionFlow.Hosting.Tests
{
    public class StartupReportHostedServiceTests
    {
        // AC-008.1
        [Fact]
        public async Task StartAsync_LogsEachHandler_WithReason()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            planner.Add<Dependent>().DependsOn<EveryMinute>().RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(10));
            planner.Add<EveryMinuteB>().Enabled(false);
            planner.Add<Dependent2>().DependsOn<EveryMinuteB>();
            planner.Add<DependentOwn>().DependsOn<EveryMinute>().RunMode(DependentRunMode.OwnSchedule);
            var plan = planner.Build(MissingPrerequisite.SkipDependents);

            await using var host = new TestHost(o =>
            {
                o.Add(typeof(Hourly));
                o.Add(typeof(TenAmSaoPaulo));
                o.UsePlan(plan);
                o.GetPlanOf<Hourly>().Enabled(false);
            });
            await host.StartAsync();

            var lines = host.Logs.At(LogLevel.Information).ToList();
            string Line<T>() => Assert.Single(lines, l => l.Contains($"'{typeof(T).FullName}' is"));

            Assert.Equal(7, lines.Count);
            Assert.Contains("is scheduled every 00:01:00", Line<EveryMinute>());
            Assert.Contains("triggered by its prerequisites", Line<Dependent>());
            Assert.Contains("00:10:00", Line<Dependent>());
            Assert.Contains("not scheduled: disabled in the execution plan", Line<EveryMinuteB>());
            Assert.Contains("not scheduled: prerequisite 'EveryMinuteB'", Line<Dependent2>());
            Assert.Contains("SkipDependents", Line<Dependent2>());
            Assert.Contains("after a new cycle of", Line<DependentOwn>());
            Assert.Contains("not scheduled: disabled (GetPlanOf().Enabled(false)", Line<Hourly>());
            Assert.Contains("on the cron '0 10 * * *' (America/Sao_Paulo)", Line<TenAmSaoPaulo>());
        }
    }
}
