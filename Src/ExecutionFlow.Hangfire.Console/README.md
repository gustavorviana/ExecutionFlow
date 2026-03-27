# ExecutionFlow.Hangfire.Console

[Hangfire.Console](https://github.com/pieceofsummer/Hangfire.Console) integration for the [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) framework - structured logging and progress bars in the Hangfire Dashboard.

## Installation

```bash
dotnet add package ExecutionFlow.Hangfire.Console
```

## Setup

```csharp
options.ConfigureConsole();
```

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
