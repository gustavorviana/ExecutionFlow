using ExecutionFlow.Hangfire;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// Hangfire server (infrastructure only — needed to process jobs from the queue)
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

var host = builder.Build();

// ExecutionFlow — configured MANUALLY, no handlers or services registered in DI
var setup = new HangfireSetup();
setup.Configure(options =>
{
    options.Scan(typeof(Program).Assembly);
});
var dispatcher = setup.ConfigureActivator().Build();

Console.WriteLine("===========================================");
Console.WriteLine("  ExecutionFlow Consumer (sem DI) Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("===========================================");

host.Run();
