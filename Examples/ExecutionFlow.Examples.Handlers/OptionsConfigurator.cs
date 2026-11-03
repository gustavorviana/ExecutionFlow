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
            options.SetJobIdGeneratorType<ExampleJobIdGenerator>();
            options.Scan(typeof(IHandlerMark).Assembly);
            options.GetPlanOf<AutoRunDisabledHandler>().Enabled(false);
            options.AddStateHandler<Hooks.JobMonitor>();

            // Dependencies between recurring handlers: registers the plan's handlers and their run modes.
            options.UsePlan(CatalogPlan.Build());
        }
    }
}
