# ExecutionFlow.Extensions.Logging

Sends [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) handler logs (`context.Log`) to the application's `ILogger` (Microsoft.Extensions.Logging), so they reach Serilog, Application Insights, OpenTelemetry or any other provider you already use.

## Installation

```bash
dotnet add package ExecutionFlow.Extensions.Logging
```

## Setup

```csharp
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(MyHandler).Assembly);
    options.AddMicrosoftLogging();   // needs an ILoggerFactory in the container (the host provides one)
});
```

It works alongside other loggers, e.g. `options.ConfigureConsole()` for the Hangfire dashboard.

## What you get

```csharp
public Task HandleAsync(FlowContext<OrderPaid> context, CancellationToken ct)
{
    context.Log.Info("Order {OrderId} paid by {Customer}", context.Event.OrderId, context.Event.Customer);
    return Task.CompletedTask;
}
```

- **Category:** the handler type's full name (or the event type when the handler isn't registered on this host).
- **Scope:** every entry carries `JobId` and `AttemptNumber`.
- **Structured logging:** the message is passed as a template, so `OrderId` and `Customer` stay named properties.
- **Levels:** `Trace`, `Debug`, `Information`, `Warning`, `Error` and `Critical` map one to one; `Success` maps to `Information`.
- A logger that throws never fails the job: ExecutionFlow isolates each logger.

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core abstractions |
| **ExecutionFlow.Hangfire** | Hangfire integration |
| **ExecutionFlow.Hangfire.Console** | Logging and progress bars in the Hangfire dashboard |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core DI extensions |
