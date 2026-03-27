# ExecutionFlow.Hangfire

Hangfire integration for the [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) framework - dispatching, filters, execution manager, and job lifecycle management.

## Installation

```bash
dotnet add package ExecutionFlow.Hangfire
```

## Features

- Event dispatching (fire-and-forget, scheduled, delayed)
- Execution manager (track, cancel, retry jobs)
- Custom job IDs and display names
- Deduplication (skip or replace existing jobs)
- Recurring job management
- Lifecycle hooks
- Hangfire native attribute propagation

## Usage

### Dispatching

```csharp
// Fire-and-forget
dispatcher.Publish(new SendEmailEvent { To = "user@mail.com" });

// Delayed
dispatcher.Schedule(new SendReminderEvent(), TimeSpan.FromMinutes(30));

// Scheduled
dispatcher.Schedule(new SendReportEvent(), new DateTimeOffset(2025, 12, 31, 9, 0, 0, TimeSpan.Zero));
```

### Publish Result

```csharp
var result = dispatcher.Publish(event);
result.JobId;    // custom ID if available, otherwise internal job ID
result.Enqueued; // true if job was actually enqueued
```

### Custom ID (Job Tracking)

```csharp
public class PaymentEvent : ICustomIdEvent
{
    public string OrderId { get; set; }
    public string CustomId => $"payment-{OrderId}";
}

executionManager.IsRunning("payment-123");
executionManager.Cancel("payment-123");
executionManager.Retry("payment-123");
```

### Deduplication

```csharp
options.DeduplicationBehavior = DeduplicationBehavior.SkipIfExists;
```

| Behavior | When duplicate exists |
|---|---|
| `Disabled` (default) | Always enqueues |
| `SkipIfExists` | Returns `Enqueued = false` |
| `ReplaceExisting` | Cancels existing, enqueues new |

### Lifecycle Hooks

```csharp
public class JobMonitor : IOnFailed, IOnSucceeded
{
    public void OnFailed(ExecutionFailedEvent e) { /* e.Exception, e.Duration, e.JobId */ }
    public void OnSucceeded(ExecutionSucceededEvent e) { /* e.Duration */ }
}

options.AddStateHandler<JobMonitor>();
```

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core abstractions |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core DI extensions |
| **ExecutionFlow.Hangfire.Console** | Console logging + progress bars |
