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
            options.Scan(typeof(IHandlerMark).Assembly);
            options.SetJobAutoRun<AutoRunDisabledHandler>(false);
        }
    }
}
