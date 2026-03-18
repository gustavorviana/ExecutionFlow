using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Handlers;
using ExecutionFlow.Hangfire;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// Hangfire server (processes jobs from the queue)
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// Register handlers in DI
builder.Services.AddTransient<SendMessageHandler>();
builder.Services.AddTransient<HeartbeatHandler>();

// Register ExecutionFlow setup (HangfireSetup implements IExecutionFlowSetup)
builder.Services.AddSingleton<HangfireSetup>();
builder.Services.AddSingleton<IExecutionFlowRegistry>(sp => sp.GetRequiredService<HangfireSetup>());

var host = builder.Build();

// Configure — single call, scans handlers + sets up Hangfire
var setup = host.Services.GetRequiredService<HangfireSetup>();
setup.Configure(options => options.Scan(typeof(IHandlerMark).Assembly));

Console.WriteLine("===========================================");
Console.WriteLine("  ExecutionFlow Console Worker Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("===========================================");

host.Run();
