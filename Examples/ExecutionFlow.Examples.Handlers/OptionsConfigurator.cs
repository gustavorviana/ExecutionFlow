using ExecutionFlow.Examples.Handlers.Recurring;
using ExecutionFlow.Examples.Handlers.Recurring.Plan;
using ExecutionFlow.Hangfire;
using ExecutionFlow.Hangfire.Console;

namespace ExecutionFlow.Examples.Handlers
{
    public static class OptionsConfigurator
    {
        public static void Configure(HangfireOptions options)
        {
            options.ConfigureConsole();
            options.RemoveOrphanRecurringJobs = true;

            // Hangfire checks recurring jobs once per BackgroundJobServerOptions.SchedulePollingInterval (15 s by default).
            // Keep this equal to the servers' value: schedules more frequent than it get a warning, and intervals with
            // seconds are raised to it (see IntervalPulseHandler).
            options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            options.SetJobIdGeneratorType<ExampleJobIdGenerator>();
            options.Scan(typeof(IHandlerMark).Assembly);
            options.GetPlanOf<AutoRunDisabledHandler>().Enabled(false);
            options.AddStateHandler<Hooks.JobMonitor>();

            // Dependencies between recurring handlers: registers the plan's handlers and their run modes.
            options.UsePlan(CatalogPlan.Build());
        }
    }
}
