using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Handlers;
using ExecutionFlow.Examples.Handlers.Recurring;
using ExecutionFlow.Examples.Shared.Events;
using ExecutionFlow.Hangfire;
using ExecutionFlow.Hangfire.DependencyInjection;
using Hangfire;
using Hangfire.Console;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// Hangfire server (processes jobs from the queue)
builder.Services.AddHangfire(config =>
    config
        .UseSqlServerStorage(connectionString)
        .UseConsole());

builder.Services.AddHangfireServer();

// Register ExecutionFlow with all handlers via DI
builder.Services.AddHangfireToExecutionFlow(OptionsConfigurator.Configure);

var app = builder.Build();

Console.WriteLine("===========================================");
Console.WriteLine("  ExecutionFlow Console Worker Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("===========================================");

// ---------------------------------------------------------------------------------------------------------------------
// Publishing and managing jobs. Try: POST /api/orders/42, GET /api/jobs/order-42, DELETE /api/jobs/order-42.
// ---------------------------------------------------------------------------------------------------------------------
var api = app.MapGroup("/api");

// Publish (deduplicated by custom ID: a second publish while order-42 is active returns 409) and schedule.
api.MapPost("/orders/{id}", (string id, IEventDispatcher dispatcher) =>
{
    var result = dispatcher.Publish(new OrderCreatedEvent { OrderId = id });
    return result.Enqueued ? Results.Accepted(value: new { result.JobId }) : Results.Conflict($"order-{id} is already active");
});
api.MapPost("/orders/{id}/schedule", (string id, int seconds, IEventDispatcher dispatcher) =>
    Results.Ok(new { dispatcher.Schedule(new OrderCreatedEvent { OrderId = id }, TimeSpan.FromSeconds(seconds)).JobId }));

// Jobs by Hangfire job ID or custom ID ("order-42").
api.MapGet("/jobs/{id}", (string id, IExecutionManager jobs) => new { Running = jobs.IsRunning(id), Pending = jobs.IsPending(id) });
api.MapDelete("/jobs/{id}", (string id, IExecutionManager jobs) => new { Cancelled = jobs.Cancel(id) });      // also scheduled jobs
api.MapPost("/jobs/{id}/retry", (string id, IExecutionManager jobs) => new { Retried = jobs.Retry(id) });   // a failed job

// Listing and counting.
api.MapGet("/jobs", (JobState state, IExecutionManager jobs) => jobs.GetJobs(state).Take(50)
    .Select(j => new { j.JobId, j.CustomId, Type = j.EventTypeName ?? j.HandlerType?.Name, j.State, j.StateChangedAt })
    .ToList());
api.MapGet("/jobs/summary", (IExecutionManager jobs) => jobs.GetStateSummary());

// Recurring handlers: run now (even when turned off) and check or cancel by handler type.
api.MapPost("/recurring/data-sync/run", (IRecurringTrigger trigger) =>
{
    trigger.Trigger(typeof(DataSyncHandler));
    return Results.Accepted();
});
api.MapGet("/recurring/data-sync", (IExecutionManager jobs) => new
{
    Running = jobs.IsRunning(typeof(DataSyncHandler)),
    Pending = jobs.IsPending(typeof(DataSyncHandler)),
});
api.MapDelete("/recurring/data-sync", (IExecutionManager jobs) => new { Cancelled = jobs.Cancel(typeof(DataSyncHandler)) });

app.UseHangfireDashboard("", options: new DashboardOptions().UseExecutionFlowJobNames(app.Services));

app.Run();