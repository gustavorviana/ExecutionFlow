# ExecutionFlow

Core abstractions for the ExecutionFlow framework - a lightweight, handler-based background job execution layer for .NET.

## Features

- **Zero external dependencies** - pure .NET Standard 2.0
- Handler-based architecture (`IHandler<TEvent>`, `IHandler`)
- Lifecycle hooks (`IOnFailed`, `IOnSucceeded`, `IOnRetrying`, etc.)
- Custom job IDs and display names
- Deduplication support
- Flow parameters

## Installation

```bash
dotnet add package ExecutionFlow
```

## Quick Start

### Define an event and handler

```csharp
public class SendEmailEvent
{
    public string To { get; set; }
    public string Subject { get; set; }
}

public class SendEmailHandler : IHandler<SendEmailEvent>
{
    public async Task HandleAsync(FlowContext<SendEmailEvent> context, CancellationToken ct)
    {
        var email = context.Event;
        context.Log.Info($"Sending email to {email.To}");
    }
}
```

### Recurring handler

```csharp
[Recurring("*/5 * * * *")]
[DisplayName("Data Sync")]
public class DataSyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken ct)
    {
        context.Log.Info("Syncing data...");
    }
}
```

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow.Hangfire** | Hangfire integration |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core DI extensions |
| **ExecutionFlow.Hangfire.Console** | Console logging + progress bars |
