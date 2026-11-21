using ExecutionFlow.Examples.Configuration.Examples;
using ExecutionFlow.Examples.Configuration.Verification;
using ExecutionFlow.Hangfire;
using System.Diagnostics;

// Every way to configure recurring handlers. Each example (one file in Examples/) shows the configuration an app would
// write, and checks what it resolves to. No storage is needed. The process exits with 1 if any check fails.
Trace.Listeners.Add(new ConsoleTraceListener());   // shows the configuration warnings

IConfigurationExample[] examples =
{
    new AttributesOnlyExample(),
    new GetPlanOfExample(),
    new InlinePlannerExample(),
    new ConfigurationClassesExample(),
    new UsePlanFromAssemblyExample(),
    new ConsumerConfigurationExample(),
};

var expect = new Expectations();

foreach (var example in examples)
{
    Console.WriteLine();
    Console.WriteLine($"== {example.Title}");

    var setup = new HangfireSetup();
    setup.Configure(example.Configure);
    example.Verify(expect, new ResolvedConfiguration(setup));
}

Console.WriteLine();
Console.WriteLine(expect.Failures == 0 ? "All configuration examples behave as documented." : $"{expect.Failures} check(s) failed.");
return expect.Failures == 0 ? 0 : 1;
