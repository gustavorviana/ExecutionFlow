using ExecutionFlow.Examples.Handlers;
using ExecutionFlow.Hangfire;
using Hangfire;
using Hangfire.Console;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

GlobalConfiguration.Configuration
    .UseSqlServerStorage(connectionString)
    .UseConsole();

var setup = new HangfireSetup();
setup.Configure(OptionsConfigurator.Configure);
setup.ConfigureActivator().Build();

Console.WriteLine("=============================================");
Console.WriteLine("  ExecutionFlow Consumer (without DI) Started");
Console.WriteLine("  Waiting for messages...");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("=============================================");

using var server = new BackgroundJobServer();

var host = builder.Build();

host.Run();
await server.WaitForShutdownAsync(default);