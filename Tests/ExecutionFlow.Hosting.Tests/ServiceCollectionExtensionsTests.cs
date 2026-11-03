using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using ExecutionFlow.Hosting.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ExecutionFlow.Hosting.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        // AC-001.1
        [Fact]
        public void AddExecutionFlowHosting_RegistersHandlersPlanAndRunner()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>();
            planner.Add<Dependent>().DependsOn<EveryMinute>();
            var plan = planner.Build();

            var services = new ServiceCollection();
            services.AddSingleton(new RunProbe());
            services.AddExecutionFlowHosting(o =>
            {
                o.Add(typeof(Hourly));
                o.UsePlan(plan);
            });

            foreach (var handlerType in new[] { typeof(Hourly), typeof(EveryMinute), typeof(Dependent) })
                Assert.Equal(ServiceLifetime.Scoped, services.Single(d => d.ServiceType == handlerType).Lifetime);

            using var provider = services.BuildServiceProvider();
            // The registered plan is the one re-evaluated with the provider's on/off (same steps).
            Assert.Equal(plan.Steps.Select(s => s.HandlerType), provider.GetRequiredService<ExecutionPlan>().Steps.Select(s => s.HandlerType));
            Assert.IsType<ExecutionPlanRunner>(provider.GetRequiredService<IExecutionPlanRunner>());
        }

        [Fact]
        public void AddExecutionFlowHosting_IgnoresEventHandlers()
        {
            var services = new ServiceCollection();
            services.AddExecutionFlowHosting(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(SomeEventHandler));
            });

            Assert.DoesNotContain(services, d => d.ServiceType == typeof(SomeEventHandler));
        }

        // AC-001.3
        [Fact]
        public void AddExecutionFlowHosting_Throws_WhenHandlerHasNoSchedule()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new ServiceCollection().AddExecutionFlowHosting(o => o.Add(typeof(Unscheduled))));

            Assert.Contains(typeof(Unscheduled).FullName!, ex.Message);
        }

        // AC-001.4
        [Fact]
        public void AddExecutionFlowHosting_Throws_WhenCalledTwice()
        {
            var services = new ServiceCollection();
            services.AddExecutionFlowHosting(o => o.Add(typeof(EveryMinute)));

            Assert.Throws<InvalidOperationException>(() => services.AddExecutionFlowHosting(o => o.Add(typeof(EveryMinute))));
        }

        // AC-001.5
        [Fact]
        public void AddExecutionFlowHosting_Throws_WhenTimeZoneNotFound()
        {
            var global = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddExecutionFlowHosting(o =>
            {
                o.Add(typeof(Hourly));
                o.RecurringTimeZone = "Mars/Olympus_Mons";
            }));
            Assert.Contains("Mars/Olympus_Mons", global.Message);

            var perHandler = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddExecutionFlowHosting(o =>
            {
                o.Add(typeof(Hourly));
                o.GetPlanOf<Hourly>().TimeZone("Mars/Olympus_Mons");
            }));
            Assert.Contains("Mars/Olympus_Mons", perHandler.Message);
        }

        // AC-002.1
        [Fact]
        public async Task AddExecutionFlowHosting_DoesNotScheduleDisabledHandler()
        {
            await using var host = new TestHost(o =>
            {
                o.Add(typeof(EveryMinute));
                o.Add(typeof(EveryMinuteB));
                o.GetPlanOf<EveryMinute>().Enabled(false);
            });

            Assert.Single(host.HostedServices.OfType<RecurringHandlerHost>());

            // Disabled handlers are still registered.
            Assert.NotNull(host.Provider.CreateScope().ServiceProvider.GetService(typeof(EveryMinute)));

            await host.StartAsync();
            await host.Advance(TimeSpan.FromMinutes(2));
            await Wait.Until(() => host.Probe.Ended<EveryMinuteB>() == 2, "the enabled handler ran");
            Assert.Equal(0, host.Probe.Started<EveryMinute>());
        }

        // AC-002.3
        [Fact]
        public void AddExecutionFlowHosting_Throws_WhenGetPlanOfReferencesUnknownHandler()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddExecutionFlowHosting(o =>
            {
                o.Add(typeof(EveryMinute));
                o.GetPlanOf<EveryMinuteB>().Enabled(false);
            }));

            Assert.Contains(typeof(EveryMinuteB).FullName!, ex.Message);
        }

        // AC-002.5
        [Fact]
        public async Task AddExecutionFlowHosting_UsesPlanEnabled_ForPlanHandlers()
        {
            var planner = new ExecutionPlanner();
            planner.Add<EveryMinute>().Enabled(false);
            planner.Add<EveryMinuteB>();
            var plan = planner.Build();

            await using var host = new TestHost(o =>
            {
                o.GlobalRecurringAutoRun = true;
                o.UsePlan(plan);
            });

            var scheduled = Assert.Single(host.HostedServices.OfType<RecurringHandlerHost>());
            await host.StartAsync();
            await host.Advance(TimeSpan.FromMinutes(1));
            await Wait.Until(() => host.Probe.Ended<EveryMinuteB>() == 1, "the enabled step ran");
            Assert.Equal(0, host.Probe.Started<EveryMinute>());
        }
    }
}
