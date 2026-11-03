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
| **ExecutionFlow.Extensions.Logging** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Extensions.Logging.svg)](https://www.nuget.org/packages/ExecutionFlow.Extensions.Logging) | Sends `context.Log` to the application's `ILogger` (Serilog, Application Insights...) |
| **ExecutionFlow.Hosting** | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hosting.svg)](https://www.nuget.org/packages/ExecutionFlow.Hosting) | Runs recurring handlers inside the .NET Generic Host without Hangfire, locally and isolated ([README](Src/ExecutionFlow.Hosting/README.md)) |

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

> **Prefer DI.** Without DI, ExecutionFlow uses a small built-in activator. It's an internal fallback that only activates ExecutionFlow's handlers, hooks and loggers, not a DI container for your app (no scopes, no disposal). If your app can use Microsoft DI, use `AddHangfireToExecutionFlow` instead.

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

Show ExecutionFlow job names in the Hangfire dashboard:

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions().UseExecutionFlowJobNames(app.Services));
```

An ExecutionFlow job's name is the first of:
1. `ICustomNameEvent.CustomName` on the event (per job instance),
2. Hangfire's `[JobDisplayName]` on the handler's `HandleAsync`, where `{0}` is the event,
3. `[DisplayName]` on the handler class,
4. `[DisplayName]` on the event (or recurring handler) class, when the handler isn't registered (e.g. on producer-only hosts),
5. when no name is defined anywhere: `IJobIdGenerator.GenerateId(type)` for the handler type (or the event type when the handler isn't registered). The default generator returns the type's full name, e.g. `MyApp.Handlers.OrderReminderHandler`.

`[DisplayName]` goes on classes; only `[JobDisplayName]` goes on `HandleAsync`. Steps 1, 3, 4 and 5 live in `JobDisplayNameResolver` as protected methods; `DefaultHangfireJobName` derives from it and places step 2 between them. To customize naming while keeping the rules, derive from `JobDisplayNameResolver` (and implement `IHangfireJobName`) or from `DefaultHangfireJobName`, override `GetName`, and register your class with `options.SetJobName<T>()`.

```csharp
using ExecutionFlow.Abstractions;   // ICustomNameEvent lives in the core since 1.2.0

public class NotificationEvent : ICustomNameEvent
{
    public string UserId { get; set; }
    public string CustomName => $"Notify user {UserId}";
}

public class OrderReminderHandler : IHandler<OrderReminderEvent>
{
    [JobDisplayName("Reminder for order {0}")]   // {0} = the event (its ToString()); other placeholders are shown as written
    public Task HandleAsync(FlowContext<OrderReminderEvent> context, CancellationToken ct) { ... }
}
```

Jobs that aren't ExecutionFlow's use `[JobDisplayName]` on their method, or, without it, `IJobIdGenerator.GenerateId` of the job's type. If the dashboard can't resolve an `IHangfireJobName` from the service provider, every job keeps Hangfire's own default name (`Class.Method`).

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
        // The job failed for good (no retry follows): e.Exception, e.Duration, e.JobId, e.CustomId, e.HandlerType
        _dispatcher.Publish(new AlertAdminEvent { Error = e.Exception.Message });
    }

    public void OnSucceeded(ExecutionSucceededEvent e)
    {
        // e.Duration - how long the successful attempt took
    }

    public void OnRetrying(ExecutionRetryingEvent e)
    {
        // A failed attempt will run again: e.AttemptNumber, e.Exception, e.Duration
    }
}

// Register
options.AddStateHandler<JobMonitor>();
```

| Hook | Fires when | Extra data |
|---|---|---|
| `IOnEnqueued` | The job is enqueued (not a retry) | — |
| `IOnProcessing` | A worker starts the job | — |
| `IOnSucceeded` | The job succeeds | `Duration` |
| `IOnRetrying` | A failed attempt is scheduled to run again (once per retry), or a failed job is requeued manually | `AttemptNumber`, `Exception` (null for manual requeue), `Duration` |
| `IOnFailed` | The job fails **for good**: no retries left (including when the retry policy deletes it) | `Exception`, `Duration` |
| `IOnCancelled` | The job is deleted (cancelled) | — |

Things to know:
- **Where hooks run.** Hooks run inside Hangfire's state transition, in the process where it happens. `OnEnqueued` for a publish runs in the **producer**. Producer-only hosts (`BuildDispatcherOnly` / `AddExecutionFlowDispatcher`) fire **no** hooks. All other hooks run on the server processing the job.
- **Scheduled jobs.** A job created with `Schedule(...)` fires no hook until it's due (`OnEnqueued`). To record the schedule itself, use the `PublishResult` returned by `Schedule`.
- **Keep hooks fast.** They run inside the state transition. Hand heavy work to `Publish`, as in the example above.
- **Hook errors never affect the job.** Each hook runs in isolation: if it throws, the other hooks still run and the job's state change goes on. Errors are written with `Trace.TraceWarning`, or sent to your own handler:

```csharp
options.HookErrorHandler = error =>
    logger.LogError(error.Exception, "Hook {Hook} failed for job {JobId}", error.HookType.Name, error.Event.JobId);
```

To veto or change a job's state, write a Hangfire filter instead of a hook.

## Recurring Job Control

Every `IHandler` needs a schedule, `[Recurring("<cron>")]` or `[RecurringInterval("hh:mm:ss")]`; a recurring handler without one, or with both, fails at `Configure`.

```csharp
[Recurring("0 8 * * *", Id = "daily-report", TimeZone = "America/Sao_Paulo")]
public class DailyReportHandler : IHandler { ... }
```

- `Id` (optional): a stable recurring job ID. Without it, the ID is the handler's full type name (or your `IJobIdGenerator`), so renaming the class creates a new job. An explicit `Id` always wins over the generator.
- `TimeZone` (optional): the time zone the cron is evaluated in. Precedence: `GetPlanOf<T>().TimeZone(...)` > attribute `TimeZone` > `RecurringTimeZone` > UTC. On .NET Framework only Windows time zone IDs exist; unknown IDs fail at `Configure`.

```csharp
options.RecurringTimeZone = "America/Sao_Paulo";           // default time zone (null = UTC)
options.GetPlanOf<DailyReportHandler>().TimeZone("Europe/Lisbon"); // per handler, wins over the attribute
options.GlobalRecurringAutoRun = true;                    // default: run all on schedule
options.GetPlanOf<DataSyncHandler>().Enabled(false);      // never runs on schedule, only via Trigger
options.DisableRecurringRetries = true;                   // default: no retries for recurring
options.RemoveOrphanRecurringJobs = true;                 // remove ExecutionFlow recurring jobs no longer in code
```

Auto-run applies to the **whole storage**: a handler that doesn't auto-run is registered with a schedule that never fires (`Cron.Never()`), and the last `Build()` wins. To choose **which server processes** a recurring job, use Hangfire queues: put `[Queue("reports")]` on the handler and let only that server listen to the `reports` queue.

`RemoveOrphanRecurringJobs` only removes ExecutionFlow recurring jobs. Recurring jobs created directly with Hangfire, or by other applications sharing the storage, are left alone.

Manual trigger (works whether or not the handler auto-runs):

```csharp
trigger.Trigger(typeof(DataSyncHandler));   // uses the handler's ID (explicit Id or generated)
trigger.Trigger("my-job-id");
```

### Interval schedules

**Prefer `[Recurring]` with a cron expression on Hangfire.** `[RecurringInterval]` is an alternative for "every N of time":

```csharp
[RecurringInterval("00:15:00")]                    // every 15 minutes
[RecurringInterval("01:30:00", Id = "price-sync")] // every 90 minutes, with a stable ID
```

- The format is `[d.]hh:mm:ss` (e.g. `"10:00:00"`, `"1.00:00:00"`); the minimum is one minute. An interval has no time zone: `RecurringTimeZone` doesn't apply to it, and a per-handler time zone (`GetPlanOf<T>().TimeZone(...)`) for it is ignored with a trace warning.
- An interval with an exact cron equivalent (it divides an hour, or a day in whole hours) is registered as that cron in UTC, so it's aligned to the clock: every 15 minutes runs at :00, :15, :30, :45.
- Any other interval (7 min, 90 min, 5 h) runs on a per-minute recurring job that skips each minute until the interval has passed since the last run, with about one minute of precision. Skipped minutes create no job, so nothing shows in the dashboard. A manual `Trigger` always runs and counts as the last run.

### Overlapping runs

If a run takes longer than the schedule, Hangfire starts the next one in parallel. Add `[NoOverlap(timeoutSeconds)]` to prevent it: a run that starts while another one is in progress waits for it, and fails if the timeout passes first. The timeout is mandatory.

```csharp
[Recurring("*/5 * * * *"), NoOverlap(600)]
public class ImportHandler : IHandler { ... }
```

`[NoOverlap]` is part of the core package, so handlers don't depend on Hangfire's `[DisableConcurrentExecution]` (which still works). In an execution plan, `entry.NoOverlap(TimeSpan)` takes precedence over the attribute, and dependents never overlap.

### Execution plan

Declare which recurring handlers wait for which with `ExecutionPlanner`, and register the plan on the options with `UsePlan`:

```csharp
var planner = new ExecutionPlanner();
planner.Add<CategorySyncHandler>();
planner.Add<ProductSyncHandler>().DependsOn<CategorySyncHandler>();

options.UsePlan(planner.Build());
```

`UsePlan` lives on the core `ExecutionFlowOptions`, so the same plan works with any execution provider. See `Examples/` for every plan feature.

**Without a planner (automatic plan).** Put the dependencies on the handlers and register them with `Scan` or `Add`; when `UsePlan` isn't called and a registered handler has `[DependsOn]` or `[DependentRun]`, the registered handlers form a plan with the same rules:

```csharp
[Recurring("*/10 * * * *")]
public class CategorySyncHandler : IHandler { ... }

[DependsOn(typeof(CategorySyncHandler))]
[DependentRun(DependentRunMode.MinInterval, "00:20:00")]   // optional; or DependentRunMode.OwnSchedule
public class ProductSyncHandler : IHandler { ... }

options.Scan(typeof(CategorySyncHandler).Assembly);
options.AutomaticPlanMissingPrerequisite = MissingPrerequisite.Throw; // default; or SkipDependents
```

In the automatic plan, `GetPlanOf<T>().Enabled(...)`/`GlobalRecurringAutoRun` turn handlers on or off. With `UsePlan`, the explicit plan is the only one: `[DependsOn]` and `[DependentRun]` on handlers outside it are ignored. In a plan, `RunMode(...)` on the builder replaces the class's `[DependentRun]`.

### Handler configuration (EF Core style)

`ExecutionPlanner` is the single place to configure handlers. `Add<T>()` returns a `HandlerBuilder<T>`, and the same builder is passed to `IHandlerConfiguration<T>` classes, like EF Core's `IEntityTypeConfiguration<T>`:

```csharp
public class ProductSyncConfiguration : IHandlerConfiguration<ProductSyncHandler>
{
    public void Configure(HandlerBuilder<ProductSyncHandler> handler)
    {
        handler.Cron("*/10 * * * *")                  // or .Interval(TimeSpan.FromMinutes(90))
               .Id("product-sync")
               .TimeZone("America/Sao_Paulo")
               .DisplayName("Product Sync")
               .DependsOn<CategorySyncHandler>()     // .RemoveDependency<X>() / .ClearDependencies()
               .RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(20)) // or DependentRunMode.OwnSchedule
               .NoOverlap(TimeSpan.FromMinutes(10))  // or .AllowOverlap()
               .Queue("reports")                     // Hangfire extension
               .AutomaticRetry(3);                   // Hangfire extension
    }
}

var planner = new ExecutionPlanner();
planner.ApplyConfigurationsFromAssembly(typeof(ProductSyncConfiguration).Assembly); // or ApplyConfiguration(new ...)
planner.Add<CategorySyncHandler>().Enabled(true);                                   // inline, same builder
options.UsePlan(planner.Build());
```

Like EF's fluent API over data annotations, the builder changes **only what it calls**; every other item keeps its attribute, option or default. Configuring the same handler twice (two classes, or a class and `Add<T>()`) throws.

| Item | Default | Global option | Attribute | Per-handler option | Builder (wins) |
|---|---|---|---|---|---|
| Schedule | — | — | `[Recurring]` / `[RecurringInterval]` | — | `Cron` / `Interval` |
| Id | `IJobIdGenerator` | — | `Id = ...` | — | `Id` |
| Time zone | UTC | `RecurringTimeZone` | `TimeZone = ...` | `GetPlanOf<T>().TimeZone` | `TimeZone` |
| Display name | class name | — | `[DisplayName]` | — | `DisplayName` |
| Enabled | on | `GlobalRecurringAutoRun` | — | `GetPlanOf<T>().Enabled` | `Enabled` |
| Dependencies | none | — | `[DependsOn]` (merged) | — | `DependsOn` / `RemoveDependency` / `ClearDependencies` |
| Run mode | triggered | — | `[DependentRun]` | — | `RunMode` |
| Overlap | allowed | — | `[NoOverlap]` | — | `NoOverlap` / `AllowOverlap` |
| Queue, retries | Hangfire's, `DisableRecurringRetries` | — | `[Queue]`, `[AutomaticRetry]` | — | `Queue`, `AutomaticRetry` |

Loading only configuration classes? `UsePlanFromAssembly` builds the planner for you:

```csharp
options.UsePlanFromAssembly(typeof(ProductSyncConfiguration).Assembly,
    t => t.Namespace.EndsWith(".Hangfire"),          // optional filter
    MissingPrerequisite.Throw);                        // optional policy
```

**One plan per provider.** A configuration class doesn't say which provider runs it: each provider's options get their own plan, so filter the classes (by assembly or namespace) that belong to each one, and do the same with `Scan(assembly, predicate)`. A handler registered in two providers runs in both.

**Two planners.** Each provider's options have an internal planner: the class's attributes plus `options.GetPlanOf<T>()`, which returns the same `HandlerBuilder<T>` and wins over the attributes. It decides the handlers that aren't in your plan, and forms the automatic plan when they have dependencies. When a handler **is** in your plan (`UsePlan`, `UsePlanFromAssembly`, configuration classes), your plan replaces its `GetPlanOf` values as a whole and `Configure` writes one trace warning; the class's attributes still apply item by item. `GetPlanOf<T>()` only configures: the handler must be registered with `Scan` or `Add`.

```csharp
options.Scan(typeof(DataSyncHandler).Assembly);
options.GetPlanOf<DataSyncHandler>().Enabled(false).TimeZone("Europe/Lisbon");
```

`SetJobAutoRun<T>` and `SetJobTimeZone<T>` are obsolete shortcuts for `GetPlanOf<T>().Enabled(...)` and `.TimeZone(...)`.

## Hangfire Native Attributes

Hangfire attributes on handlers are propagated automatically:

```csharp
[Queue("critical")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
[DisableConcurrentExecution(300)]
public class ImportHandler : IHandler<ImportEvent> { ... }
```

## Flow Parameters

Every context tells the handler which job and attempt it's running, without depending on Hangfire:

```csharp
public async Task HandleAsync(FlowContext<MyEvent> context, CancellationToken ct)
{
    context.Log.Info($"Job {context.JobId}, attempt {context.AttemptNumber}");   // attempt 1 = first run, 2 = first retry

    // Hangfire-specific access, when you really need it (ExecutionFlow.Hangfire):
    var performContext = context.GetPerformContext();
}
```

`Parameters` holds values for **this execution only**: they aren't persisted, passed to retries or shared with other jobs, and the logger sees the same instance (so they're a good place for logging hints):

```csharp
context.Parameters["CorrelationId"] = Guid.NewGuid().ToString();
context.Parameters["LogType"] = "Audit";

// Infrastructure keys are read-only, in any casing:
// context.Parameters["CustomName"] = "x";   // throws InvalidOperationException
// context.Parameters["customname"] = "x";   // throws too
```

For Hangfire's `PerformContext`, use `context.GetPerformContext()`. The custom name is available on the event itself (`ICustomNameEvent`).

Each event type has exactly one handler. If an event needs several independent actions, publish one event per action: each gets its own job, retries and name.

## Scan with Filter

```csharp
options.Scan(assembly, type => type.Namespace.StartsWith("MyApp.Handlers"));
```

## Console Logging & Progress Bars

```csharp
GlobalConfiguration.Configuration.UseConsole();   // required by Hangfire.Console
options.ConfigureConsole(c => c.MinimumLevel = HandlerLogType.Information);   // default: Trace

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

Messages are `ILogger`-style templates (`"Order {OrderId}"` is filled by position; `{{`/`}}` are literal braces). Each logger is isolated: one that throws never fails the job, and registering the same logger twice keeps one.

## Logging to ILogger

```csharp
options.AddMicrosoftLogging();   // package ExecutionFlow.Extensions.Logging
```

`context.Log` then also reaches `ILogger`: the category is the handler type, the scope carries `JobId` and `AttemptNumber`, template properties stay structured, and `Success` maps to `Information`. It works together with `ConfigureConsole()`.

## Execution Manager

IDs can be a Hangfire job ID or a custom ID (the Hangfire ID is matched first).

```csharp
executionManager.IsRunning("order-123");            // processing?
executionManager.IsPending("order-123");            // waiting in a queue?
bool cancelled = executionManager.Cancel("order-123"); // deletes a scheduled, enqueued or processing job
bool retried = executionManager.Retry("order-123");    // requeues a failed job

executionManager.Cancel(typeof(DataSyncHandler));   // recurring jobs by handler type
```

`Cancel` also cancels **scheduled** jobs, e.g. one created with `Schedule(..., TimeSpan.FromDays(3))`. Passing the exact Hangfire job ID (the `JobId` from `PublishResult` when there's no custom ID) deletes it directly, without scanning.

### Listing and counting

```csharp
foreach (var job in executionManager.GetJobs(JobState.Failed).Take(50))
    Console.WriteLine($"{job.JobId} {job.CustomId} {job.EventTypeName ?? job.HandlerType?.Name}");

var summary = executionManager.GetStateSummary();   // Enqueued, Processing, Scheduled, Succeeded, Failed, Cancelled
```

`GetJobs` returns only ExecutionFlow jobs and is **lazy**: it reads from storage page by page as you enumerate, so memory stays bounded. Page with `Skip`/`Take`. Keep in mind:
- enumerating twice queries the storage twice (call `.ToList()` to reuse the result);
- a storage connection stays open while the enumeration runs, so if each item needs slow processing, call `.ToList()` first;
- it isn't a snapshot: jobs changing state during the enumeration may be skipped or repeated.

`CountJobs` and `GetStateSummary` come from Hangfire's statistics and cover the **whole storage**, including jobs not created by ExecutionFlow, so they can be higher than what `GetJobs` returns.

For recurring jobs, `JobInfo.HandlerType` is the handler, and `EventType`/`EventTypeName` are null.

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

var manager = setup.ExecutionManager;   // check, cancel or retry jobs in that storage
```

No global state is modified. The existing Hangfire in the process is not affected.

Producer-only hosts also get an `IExecutionManager` (registered by `AddExecutionFlowDispatcher`, or `setup.ExecutionManager`), so an API can cancel a scheduled job or show a job's status. Two things to know:
- a job cancelled from a producer-only host fires **no lifecycle hooks** (hooks only run where the full setup is built);
- the deduplication reservation key of a job cancelled there is released on the next publish of that custom ID (it's detected as stale), not immediately.

Use **either** `AddHangfireToExecutionFlow` (full mode, which also publishes) **or** `AddExecutionFlowDispatcher` (producer-only) in one container. Registering both throws, telling you which one to keep.

## Startup and Configuration Rules

- Options are a fixed snapshot: every option (methods **and** properties) must be set inside `Configure`; changing one afterwards throws.
- With DI, ExecutionFlow is built when the dispatcher is first resolved. Apps running a generic host (`Host.CreateApplicationBuilder`, ASP.NET Core) get that at startup automatically. Apps without a host must call it after building the provider:

```csharp
var provider = services.BuildServiceProvider();
provider.StartExecutionFlow();   // registers Hangfire filters and recurring jobs now
```

- Hangfire's filters and job activator are process-wide, so only **one full Hangfire setup** is active per process: a second full `Build()` replaces the previous one's filters (its dispatcher keeps publishing, but its hooks stop). Producer-only setups are unlimited. This limit is Hangfire's, not ExecutionFlow's.
- Without DI, call `setup.ConfigureActivator()` so Hangfire activates handlers through the setup; `Build()` and `ConfigureActivator()` share the same activator, in either order. The built-in activator is an internal fallback (registered types are singletons, other concrete types are transient, a constructor cycle throws); prefer Microsoft DI.

## Configuration Reference

| Option | Default | Description |
|---|---|---|
| `GlobalRecurringAutoRun` | `true` | Run recurring jobs on their schedule (off = `Cron.Never()`, trigger only) |
| `RecurringTimeZone` | `null` (UTC) | Default time zone for recurring schedules |
| `RemoveOrphanRecurringJobs` | `false` | Delete ExecutionFlow recurring jobs no longer in code |
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
  ExecutionFlow.Extensions.Logging/           Microsoft.Extensions.Logging (ILogger) bridge
Examples/
  ExecutionFlow.Examples.Producer/            Web API that publishes events
  ExecutionFlow.Examples.Consumer/            Hangfire server with DI
  ExecutionFlow.Examples.ConsumerWithoutDi/   Hangfire server without DI
```
