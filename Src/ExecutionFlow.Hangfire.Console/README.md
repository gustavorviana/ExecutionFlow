# ExecutionFlow.Hangfire.Console

[Hangfire.Console](https://github.com/pieceofsummer/Hangfire.Console) integration for the [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) framework - structured logging and progress bars in the Hangfire Dashboard.

## Installation

```bash
dotnet add package ExecutionFlow.Hangfire.Console
```

## Setup

Hangfire must have Hangfire.Console enabled, and `ConfigureConsole` doesn't enable it for you:

```csharp
GlobalConfiguration.Configuration.UseConsole();   // or services.AddHangfire(c => c.UseConsole())

options.ConfigureConsole(console =>
{
    console.MinimumLevel = HandlerLogType.Information;   // default: Trace (everything)
});
```

Calling `ConfigureConsole()` more than once registers the logger only once.

## Usage

### Logging

```csharp
public async Task HandleAsync(FlowContext<MyEvent> context, CancellationToken ct)
{
    context.Log.Info("Starting...");
    context.Log.Warning("Something odd");
    context.Log.Error("Failed!");
    context.Log.Success("Done!");
}
```

Messages use `ILogger`-style templates. `context.Log.Info("Order {OrderId} paid by {Customer}", id, name)` fills the placeholders by position, so the same message works in the console and in `ILogger` (see `ExecutionFlow.Extensions.Logging`). Use `{{` and `}}` for literal braces. For `MinimumLevel`, `Success` counts as `Information`.

A logger that throws never fails the job.

### Progress Bars

```csharp
var bar = context.CreateProgressBar("Processing");
for (int i = 0; i < total; i++)
    bar.SetValue(i, total);
bar.Complete();
```

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core abstractions |
| **ExecutionFlow.Hangfire** | Hangfire integration |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core DI extensions |
| **ExecutionFlow.Extensions.Logging** | Sends `context.Log` to `ILogger` |
