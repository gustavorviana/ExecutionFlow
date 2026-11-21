# ExecutionFlow.Extensions.Logging

Sends [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) handler logs (`context.Log`) to the application's `ILogger` (Microsoft.Extensions.Logging), so they reach Serilog, Application Insights, OpenTelemetry or any other provider you already use.

It depends only on the core `ExecutionFlow` package, so it works with every execution provider: Hangfire, the local host, or any future provider.

## Installation

```bash
dotnet add package ExecutionFlow.Extensions.Logging
```

## Setup

`AddMicrosoftLogging()` is an extension on `ExecutionFlowOptions`, so the call is the same in every provider. It needs an `ILoggerFactory` in the container; the .NET host provides one.

**Hangfire** (`ExecutionFlow.Hangfire.DependencyInjection`):

```csharp
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(MyHandler).Assembly);
    options.AddMicrosoftLogging();
});
```

**Local host** (`ExecutionFlow.Hosting`):

```csharp
builder.Services.AddExecutionFlowHosting(options =>
{
    options.Scan(typeof(MyHandler).Assembly);
    options.AddMicrosoftLogging();
});
```

It works alongside other loggers: `options.ConfigureConsole()` for the Hangfire dashboard, or your own `options.AddLogger<T>()`. Each line goes to every registered logger.

## What you get

```csharp
public Task HandleAsync(FlowContext<OrderPaid> context, CancellationToken ct)
{
    context.Log.Info("Order {OrderId} paid by {Customer}", context.Event.OrderId, context.Event.Customer);
    return Task.CompletedTask;
}
```

- **Category:** the handler type's full name (or the event type when the handler isn't registered on this host), so you can filter per handler, e.g. `"Logging:LogLevel:MyApp.Handlers.ImportHandler": "Debug"`.
- **Scope:** every entry carries `JobId` and `AttemptNumber`.
- **Structured logging:** the message is passed as a template, so `OrderId` and `Customer` stay named properties.
- **Levels:** `Trace`, `Debug`, `Information`, `Warning`, `Error` and `Critical` map one to one; `Success` maps to `Information`. Disabled levels are skipped before any work.
- A logger that throws never fails the job: ExecutionFlow isolates each logger.

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core: handlers, schedules, execution plan, contracts |
| **ExecutionFlow.Hangfire** | Hangfire provider |
| **ExecutionFlow.Hangfire.DependencyInjection** | Microsoft DI registration for the Hangfire provider |
| **ExecutionFlow.Hangfire.Console** | Logging and progress bars in the Hangfire dashboard |
| **ExecutionFlow.Hosting** | Local host provider: recurring handlers in the .NET Generic Host, without storage |
