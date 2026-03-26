# ExecutionFlow

A lightweight abstraction layer over [Hangfire](https://www.hangfire.io/) for structured background job execution with handler-based architecture, lifecycle hooks, and built-in logging.

## Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core abstractions - zero external dependencies |
| **ExecutionFlow.Hangfire** | Hangfire integration - dispatching, filters, execution manager |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core DI extensions |
| **ExecutionFlow.Hangfire.Console** | Console logging + progress bars (requires [Hangfire.Console](https://github.com/pieceofsummer/Hangfire.Console)) |

## Quick Start

### 1. Define an event and handler

```csharp
// Event
public class SendEmailEvent
{
    public string To { get; set; }
    public string Subject { get; set; }
}

// Handler
public class SendEmailHandler : IHandler<SendEmailEvent>
{
    public async Task HandleAsync(FlowContext<SendEmailEvent> context, CancellationToken ct)
    {
        var email = context.Event;
        context.Log.Info($"Sending email to {email.To}");
        // your logic here
        context.Log.Success("Email sent");
    }
}
```

### 2. Configure

**With DI (ASP.NET Core):**

```csharp
builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(SendEmailHandler).Assembly);
});
```

**Without DI:**

```csharp
GlobalConfiguration.Configuration.UseSqlServerStorage(connectionString);

var setup = new HangfireSetup();
setup.Configure(options => options.Scan(typeof(SendEmailHandler).Assembly));
setup.ConfigureActivator().Build();

using var server = new BackgroundJobServer();
```

### 3. Publish

```csharp
var result = dispatcher.Publish(new SendEmailEvent { To = "user@mail.com", Subject = "Hello" });
// result.JobId = "abc-123"
// result.Enqueued = true
```

## Handlers

### Event Handler (`IHandler<TEvent>`) - fire-and-forget

```csharp
public class OrderHandler : IHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(FlowContext<OrderCreatedEvent> context, CancellationToken ct)
    {
        var order = context.Event;
        context.Log.Info($"Processing order {order.Id}");
    }
}
```

### Recurring Handler (`IHandler`) - cron-based

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

## Dispatching

### Fire-and-forget

```csharp
dispatcher.Publish(new SendEmailEvent { To = "user@mail.com" });
```

### Delayed / Scheduled

```csharp
dispatcher.Schedule(new SendReminderEvent(), TimeSpan.FromMinutes(30));
dispatcher.Schedule(new SendReportEvent(), new DateTimeOffset(2025, 12, 31, 9, 0, 0, TimeSpan.Zero));
```

### Publish Result

All `Publish`/`Schedule` methods return `PublishResult`:

```csharp
var result = dispatcher.Publish(event);
result.JobId;    // Hangfire job ID (null if skipped by dedup)
result.Enqueued; // true if job was actually enqueued
```

## Custom ID (Job Tracking)

Associate domain identifiers with jobs:

```csharp
public class PaymentEvent : ICustomIdEvent
{
    public string OrderId { get; set; }
    public string CustomId => $"payment-{OrderId}";
}
```

Or set it dynamically inside the handler:

```csharp
context.SetCustomId($"payment-{context.Event.OrderId}");
```

Then track by custom ID:

```csharp
executionManager.IsRunning("payment-123");
executionManager.IsPending("payment-123");
executionManager.Cancel("payment-123");
executionManager.Retry("payment-123");  // re-enqueue a failed job
```

## Custom Display Name (Dashboard)

```csharp
public class NotificationEvent : ICustomNameEvent
{
    public string UserId { get; set; }
    public string CustomName => $"Notify user {UserId}";
}
```

## Deduplication

Prevent duplicate jobs for the same `CustomId`:

```csharp
options.DeduplicationBehavior = DeduplicationBehavior.SkipIfExists;
```

| Behavior | When duplicate exists |
|---|---|
| `Disabled` (default) | Always enqueues |
| `SkipIfExists` | Returns `Enqueued = false` |
| `ReplaceExisting` | Cancels existing, enqueues new |

```csharp
var result = dispatcher.Publish(new PaymentEvent { OrderId = "123" });
if (!result.Enqueued)
    Console.WriteLine("Job already running or pending");
```

## Lifecycle Hooks

React to job state transitions:

```csharp
public class JobMonitor : IOnFailed, IOnSucceeded, IOnRetrying
{
    private readonly IEventDispatcher _dispatcher;

    public JobMonitor(IEventDispatcher dispatcher) => _dispatcher = dispatcher;

    public void OnFailed(ExecutionFailedEvent e)
    {
        // e.Exception, e.Duration, e.JobId, e.CustomId, e.HandlerType
        _dispatcher.Publish(new AlertAdminEvent { Error = e.Exception.Message });
    }

    public void OnSucceeded(ExecutionSucceededEvent e)
    {
        // e.Duration - how long the job took
    }

    public void OnRetrying(ExecutionRetryingEvent e)
    {
        // e.AttemptNumber, e.Duration
    }
}

// Register
options.AddStateHandler<JobMonitor>();
```

Available hooks: `IOnEnqueued`, `IOnProcessing`, `IOnSucceeded`, `IOnFailed`, `IOnRetrying`, `IOnCancelled`.

All events include `Duration` (time since processing started).

## Recurring Job Control

```csharp
options.GlobalRecurringAutoRun = true;                    // default: auto-start all
options.SetJobAutoRun<DataSyncHandler>(false);            // disable specific handler
options.DisableRecurringRetries = true;                   // default: no retries for recurring
options.RemoveOrphanRecurringJobs = true;                 // clean up jobs not in code
```

Manual trigger:

```csharp
trigger.Trigger(typeof(DataSyncHandler));
trigger.Trigger("my-job-id");
```

## Hangfire Native Attributes

Hangfire attributes on handlers are propagated automatically:

```csharp
[Queue("critical")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
[DisableConcurrentExecution(300)]
[Timeout("00:10:00")]
public class ImportHandler : IHandler<ImportEvent> { ... }
```

## Flow Parameters

Handlers can read infrastructure parameters and set custom ones during execution:

```csharp
public async Task HandleAsync(FlowContext<MyEvent> context, CancellationToken ct)
{
    context.Parameters["CorrelationId"] = Guid.NewGuid().ToString();
    context.Parameters["LogType"] = "Audit";

    // Infrastructure parameters are read-only
    // context.Parameters["PerformContext"] = null; // throws InvalidOperationException
}
```

## Scan with Filter

```csharp
options.Scan(assembly, type => type.Namespace.StartsWith("MyApp.Handlers"));
```

## Console Logging & Progress Bars

```csharp
options.ConfigureConsole();

// In handler
context.Log.Info("Starting...");
context.Log.Warning("Something odd");
context.Log.Error("Failed!");
context.Log.Success("Done!");

var bar = context.CreateProgressBar("Processing");
for (int i = 0; i < total; i++)
    bar.SetValue(i, total);
bar.Complete();
```

## Execution Manager

```csharp
executionManager.IsRunning("order-123");
executionManager.IsPending("order-123");
executionManager.Cancel("order-123");
executionManager.Retry("order-123");

var failedJobs = executionManager.GetJobs(JobState.Failed);
```

## Producer-Only (Isolated)

Publish jobs to a separate database without affecting an existing Hangfire instance in the same process. No global filters, recurring jobs, or server are registered.

**With DI:**

```csharp
var separateStorage = new SqlServerStorage("Server=...;Database=SeparateDb;...");

// Uses a separate storage - does not interfere with existing Hangfire
builder.Services.AddExecutionFlowDispatcher(_ => separateStorage);

// Or resolve storage from DI (when JobStorage is already registered)
builder.Services.AddExecutionFlowDispatcher();
```

**Without DI:**

```csharp
var storage = new SqlServerStorage("Server=...;Database=SeparateDb;...");

var setup = new HangfireSetup();
setup.Configure(options => { });
var dispatcher = setup.BuildDispatcherOnly(storage);

dispatcher.Publish(new MyEvent());
dispatcher.Schedule(new MyEvent(), TimeSpan.FromHours(1));
```

No global state is modified. The existing Hangfire in the process is not affected.

## Configuration Reference

| Option | Default | Description |
|---|---|---|
| `GlobalRecurringAutoRun` | `true` | Auto-start recurring jobs |
| `RemoveOrphanRecurringJobs` | `false` | Delete recurring jobs not in code |
| `DisableRecurringRetries` | `true` | No retries for recurring jobs |
| `DeduplicationBehavior` | `Disabled` | Duplicate job handling strategy |

## Project Structure

```
Src/
  ExecutionFlow/                              Core abstractions
  ExecutionFlow.Hangfire/                     Hangfire integration
  ExecutionFlow.Hangfire.Console/             Console logging + progress bars
  ExecutionFlow.Hangfire.DependencyInjection/ Microsoft DI integration
Examples/
  ExecutionFlow.Examples.Producer/            Web API that publishes events
  ExecutionFlow.Examples.Consumer/            Hangfire server with DI
  ExecutionFlow.Examples.ConsumerWithoutDi/   Hangfire server without DI
```
