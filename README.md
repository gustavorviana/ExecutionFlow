# ExecutionFlow

[![.NET](https://github.com/gustavorviana/ExecutionFlow/actions/workflows/dotnet.yml/badge.svg)](https://github.com/gustavorviana/ExecutionFlow/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.svg)](https://www.nuget.org/packages/ExecutionFlow)
[![License](https://img.shields.io/github/license/gustavorviana/ExecutionFlow)](https://github.com/gustavorviana/ExecutionFlow/blob/main/LICENSE)

A lightweight abstraction layer over [Hangfire](https://www.hangfire.io/) for structured background job execution with handler-based architecture, lifecycle hooks, and built-in logging.

## Packages

| Package | Version | Description |
|---------|---------|-------------|
| **ExecutionFlow** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.svg)](https://www.nuget.org/packages/ExecutionFlow) | Core abstractions - zero external dependencies |
| **ExecutionFlow.Hangfire** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire) | Hangfire integration - dispatching, filters, execution manager |
| **ExecutionFlow.Hangfire.DependencyInjection** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.DependencyInjection.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire.DependencyInjection) | ASP.NET Core DI extensions |
| **ExecutionFlow.Hangfire.Console** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.Console.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire.Console) | Console logging + progress bars (requires [Hangfire.Console](https://github.com/pieceofsummer/Hangfire.Console)) |

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
// result.JobId   = Hangfire job ID (or custom ID if event implements ICustomIdEvent)
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

Passing a `null` event throws `ArgumentNullException`.

### Handler Routing

The handler is chosen by the **compile-time type** of the event (the generic `TEvent`), not by its runtime type. Only exact matches count, so base classes and interfaces aren't searched:

```csharp
// Only IHandler<OrderCreated> is registered. OrderCreatedExpress : OrderCreated.
dispatcher.Publish(new OrderCreated());          // runs the OrderCreated handler
OrderCreated e = new OrderCreatedExpress();
dispatcher.Publish(e);                           // runs the OrderCreated handler
dispatcher.Publish(new OrderCreatedExpress());   // fails when the job runs: no handler for OrderCreatedExpress
object o = new OrderCreated();
dispatcher.Publish(o);                           // fails when the job runs: no handler for System.Object
```

When publishing from generic code (e.g. a list of `object` or of an interface type), call `Publish` with the concrete type.

By default, a job whose event type has no registered handler on the consumer **fails without retries**. Set `RetryUnregisteredEventJobs = true` if consumers with different handlers share a queue.

### Publish Result

All `Publish`/`Schedule` methods return `PublishResult`:

```csharp
var result = dispatcher.Publish(event);
result.JobId;    // non-empty custom ID if available, otherwise internal job ID (null if skipped)
result.Enqueued; // true if job was actually enqueued
```

`Enqueued` is `false` only when the event implements `ICustomIdEvent` with a non-empty `CustomId`, deduplication is enabled, and a job with the same custom ID is already running or pending.

## Custom ID (Job Tracking)

Associate domain identifiers with jobs:

```csharp
public class PaymentEvent : ICustomIdEvent
{
    public string OrderId { get; set; }
    public string CustomId => $"payment-{OrderId}";
}
```

A `null` or empty `CustomId` means the event has no custom ID: `JobId` is the internal job ID and deduplication doesn't apply.

The custom ID is stored with the job in the same storage operation, so a job never exists without it. This holds for Hangfire's default `BackgroundJobClient`. A custom `IBackgroundJobClient` that doesn't implement `IBackgroundJobClientV2` falls back to storing it right after the job is created.

> `FlowContext.SetCustomId` is obsolete since 1.2.0 and will be removed in 2.0. Set the custom ID on the event through `ICustomIdEvent`: deduplication only uses the ID given at publish time.

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

Prevent a second job for the same `CustomId` while one is still **active**. A job is active while it's enqueued, scheduled (including a failed job waiting for an automatic retry), awaiting or processing.

Set a global default:

```csharp
using ExecutionFlow.Abstractions;

options.DeduplicationBehavior = DeduplicationBehavior.SkipIfExists;
```

Or set it per event type. The attribute wins over the global default:

```csharp
using ExecutionFlow.Attributes;

[Deduplication(DeduplicationBehavior.ReplaceExisting)]
public class RecalculateCartEvent : ICustomIdEvent
{
    public string CartId { get; set; }
    public string CustomId => $"cart-{CartId}";
}
```

| Behavior | When an active job with the same custom ID exists |
|---|---|
| `Disabled` (default) | Always enqueues |
| `SkipIfExists` | Returns `Enqueued = false` |
| `ReplaceExisting` | Deletes the active job, enqueues the new one |

```csharp
var result = dispatcher.Publish(new PaymentEvent { OrderId = "123" });
if (!result.Enqueued)
    Console.WriteLine("An active job with this custom ID already exists");
```

How it works: each custom ID gets a reservation key in Hangfire storage, checked and written under a short distributed lock per custom ID. Publishes of different custom IDs never wait on each other. If the lock isn't acquired within `DeduplicationLockTimeout` (default 1s), `Publish` throws `DistributedLockTimeoutException`, unless `CreateOnDeduplicationLockTimeout = true`, in which case the job is created without deduplication and a warning is logged.

Keep in mind:
- `ReplaceExisting` on a job that is already **processing** marks it deleted, but the running handler only stops if it observes its `CancellationToken`. Otherwise both run.
- If the process crashes between creating the job and writing the reservation key, one duplicate is possible. Handlers that must not run twice should be idempotent.
- Jobs created before 1.2.0 have no reservation key, so deduplication doesn't see them.

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
| `DeduplicationBehavior` | `Disabled` | Default duplicate handling (override per event with `[Deduplication]`) |
| `DeduplicationLockTimeout` | `1s` | Max wait for the per-custom-ID deduplication lock |
| `CreateOnDeduplicationLockTimeout` | `false` | Create the job anyway when the lock times out, instead of throwing |
| `RetryUnregisteredEventJobs` | `false` | Retry event jobs whose event type has no handler on the consumer |

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
