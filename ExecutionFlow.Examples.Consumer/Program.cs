using ExecutionFlow.Examples.Handlers;
using ExecutionFlow.Hangfire.DependencyInjection;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// Hangfire server (processes jobs from the queue)
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// Register ExecutionFlow with all handlers via DI
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
});

var host = builder.Build();

Console.WriteLine("===========================================");
Console.WriteLine("  ExecutionFlow Console Worker Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("===========================================");

host.Run();
