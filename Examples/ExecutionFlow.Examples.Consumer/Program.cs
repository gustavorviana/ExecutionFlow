using ExecutionFlow.Examples.Handlers;
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
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.RemoveOrphanRecurringJobs = true;
    options.Scan(typeof(IHandlerMark).Assembly);
});

var app = builder.Build();

Console.WriteLine("===========================================");
Console.WriteLine("  ExecutionFlow Console Worker Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("===========================================");

app.UseHangfireDashboard("", options: new DashboardOptions
{
    DisplayNameFunc = (context, job) => 
    app.Services.GetRequiredService<IHangfireJobName>().GetName(job),
});

app.Run();