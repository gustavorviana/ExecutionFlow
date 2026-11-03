using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Hosting;
using ExecutionFlow.Examples.Hosting.Handlers;
using ExecutionFlow.Hosting;
using ExecutionFlow.Hosting.Abstractions;

// Runs recurring handlers inside this app with ExecutionFlow.Hosting: no Hangfire, no storage. Locally and isolated: another
// process running this example has its own schedules and plan cycles.
//
//   dotnet run                -> scheduled mode; open http://localhost:5299 to watch the runs
//   dotnet run -- --run-once  -> runs every handler once in plan order and exits (exit code 1 if any failed)
var runOnce = args.Contains("--run-once");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RunLog>();

// The plan: FetchRates triggers RecalculatePrices (at least 20 s apart); PublishReport keeps its own schedule but only runs
// after a new cycle of RecalculatePrices. Backup skips an occurrence while its previous run is still running.
var planner = new ExecutionPlanner();
planner.Add<FetchRatesHandler>();
planner.Add<RecalculatePricesHandler>()
    .DependsOn<FetchRatesHandler>()
    .RunMode(DependentRunMode.MinInterval, TimeSpan.FromSeconds(20));
planner.Add<PublishReportHandler>()
    .DependsOn<RecalculatePricesHandler>()
    .RunMode(DependentRunMode.OwnSchedule);
planner.Add<BackupHandler>()
    .SkipWhenOverlapping();
var plan = planner.Build();

builder.Services.AddExecutionFlowHosting(options =>
{
    options.UsePlan(plan);

    // Outside the plan: FlakyImport runs on its attribute's cron; Cleanup is registered but turned off.
    options.Add(typeof(FlakyImportHandler));
    options.Add(typeof(CleanupHandler));
    options.GetPlanOf<CleanupHandler>().Enabled(false);

    options.MaxConcurrency = 4;
    options.RunOnce = runOnce;
});

var app = builder.Build();

var api = app.MapGroup("/api");

// The runs recorded by the handlers, newest first.
api.MapGet("/runs", (RunLog log, int? take) => log.Recent(take ?? 100));

// The plan as the local host applies it.
api.MapGet("/plan", (ExecutionPlan resolvedPlan) => resolvedPlan.Steps.Select(s => new
{
    Handler = s.HandlerType.Name,
    s.Enabled,
    s.SkipReason,
    Prerequisites = s.Prerequisites.Select(p => p.Name),
    Mode = s.Prerequisites.Count == 0 ? "schedule" : s.RunsOnOwnSchedule ? "own schedule, gated" : "triggered",
    s.MinInterval,
}));

// Runs every handler once, in plan order, next to the schedules (the local host has no manual trigger per handler).
api.MapPost("/run-once", async (IExecutionPlanRunner runner, CancellationToken ct) =>
{
    var result = await runner.RunOnceAsync(ct);
    return new
    {
        result.HasFailures,
        Outcomes = result.Outcomes.Select(o => new { Handler = o.HandlerType.Name, Status = o.Status.ToString(), o.Reason }),
    };
});

app.MapGet("/", () => Results.Content(Page.Html, "text/html"));

app.Run();
