# ExecutionFlow

A .NET library for defining, dispatching, and managing background jobs on top of [Hangfire](https://www.hangfire.io/) with typed handler contracts, automatic discovery, and lifecycle hooks.

## Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core contracts and abstractions — zero external dependencies. |
| **ExecutionFlow.Hangfire** | Hangfire provider — handler registration, dispatching, state filters, and execution manager. |
| **ExecutionFlow.Hangfire.DependencyInjection** | ASP.NET Core dependency injection extensions. |
| **ExecutionFlow.Hangfire.Console** | Optional progress-bar reporting on the Hangfire dashboard (requires [Hangfire.Console](https://github.com/pieceofsummer/Hangfire.Console)). |

## Recommended Project Structure

When producer and consumer run as separate processes, create a dedicated project for your events so both sides can reference it without coupling to handler implementations:

```
YourSolution/
├── YourProject.Events/          # Event classes only (shared by producer and consumer)
│   └── SendMessageEvent.cs
├── YourProject.Handlers/        # Handler implementations (consumer only)
│   ├── SendMessageHandler.cs
│   └── HeartbeatHandler.cs
├── YourProject.Producer/        # Enqueues events (references YourProject.Events)
│   └── Program.cs
└── YourProject.Consumer/        # Processes jobs (references YourProject.Events + YourProject.Handlers)
    └── Program.cs
```

## Hangfire Configuration vs ExecutionFlow Configuration

ExecutionFlow does **not** configure Hangfire itself — it only registers handlers, dispatching, and lifecycle hooks. You are responsible for configuring Hangfire storage, server, and dashboard independently.

```csharp
// 1. Hangfire configuration (your responsibility)
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// 2. ExecutionFlow configuration (handler registration, dispatching, lifecycle)
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
});
```

> **Dashboard placement:** The Hangfire dashboard must run on a process that has ExecutionFlow handlers registered (typically the consumer), so that `IHangfireJobName` can resolve handler display names. If placed on a producer-only process without handler registration, job names won't display correctly.

## Installation

ExecutionFlow is not yet available on NuGet. For now, install by cloning the repository and adding project references:

```shell
git clone https://github.com/your-org/ExecutionFlow.git
```

Then add project references from your solution:

```shell
# Core + Hangfire provider (required)
dotnet add reference path/to/ExecutionFlow/Src/ExecutionFlow/ExecutionFlow.csproj
dotnet add reference path/to/ExecutionFlow/Src/ExecutionFlow.Hangfire/ExecutionFlow.Hangfire.csproj

# Dependency injection support (recommended for ASP.NET Core)
dotnet add reference path/to/ExecutionFlow/Src/ExecutionFlow.Hangfire.DependencyInjection/ExecutionFlow.Hangfire.DependencyInjection.csproj

# Progress bars on the dashboard (optional)
dotnet add reference path/to/ExecutionFlow/Src/ExecutionFlow.Hangfire.Console/ExecutionFlow.Hangfire.Console.csproj
```

## Handlers

ExecutionFlow supports two types of handlers:

### Event Handler (`IHandler<TEvent>`)

Processes a specific event type dispatched via `IDispatcher.Enqueue()`.

**1. Define your event class** — a simple POCO, no base class required:

```csharp
namespace YourProject.Events;

public class SendMessageEvent
{
    public string From { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
```

**2. Create the handler:**

```csharp
using ExecutionFlow.Abstractions;
using YourProject.Events;

namespace YourProject.Handlers;

public class SendMessageHandler : IHandler<SendMessageEvent>
{
    public Task HandleAsync(FlowContext<SendMessageEvent> context, CancellationToken cancellationToken)
    {
        var msg = context.Event;
        context.Log.Info($"Message received from '{msg.From}' at {msg.SentAt:HH:mm:ss}:");
        context.Log.Success(msg.Content);
        return Task.CompletedTask;
    }
}
```

The `FlowContext<TEvent>` provides:
- `context.Event` — the deserialized event instance
- `context.Log` — logger with `Info()`, `Success()`, `Warning()`, `Error()` methods
- `context.Items` — a `Dictionary<string, object>` for storing arbitrary data during execution
- `context.SetCustomId(string)` — associate a custom tracking ID with this execution

### Recurring Handler (`IHandler`)

Runs on a cron schedule. Uses the non-generic `IHandler` interface with `[Recurring]` and optionally `[DisplayName]` attributes.

```csharp
using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace YourProject.Handlers;

[Recurring("* * * * *")]   // every minute
[DisplayName("Heartbeat")]
public class HeartbeatHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Info($"Heartbeat at {DateTime.UtcNow:HH:mm:ss} UTC");
        return Task.CompletedTask;
    }
}
```

The cron expression follows the standard format: `minute hour day month day-of-week`.

## Setup with Dependency Injection

When using `AddHangfireToExecutionFlow()`, all discovered handlers (event handlers and recurring handlers) are automatically registered in the DI container as **transient** services. State handlers added via `AddStateHandler<T>()` are also resolved through the DI container, so they can receive injected dependencies in their constructors.

### Consumer (processes jobs)

```csharp
using ExecutionFlow.Hangfire;
using ExecutionFlow.Hangfire.DependencyInjection;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// --- Hangfire configuration ---
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// --- ExecutionFlow configuration ---
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly); // auto-discover all handlers
    options.RemoveOrphanRecurringJobs = true;    // clean up unregistered recurring jobs
});

var app = builder.Build();

// --- Hangfire dashboard (on the consumer so IHangfireJobName can resolve names) ---
app.UseHangfireDashboard("", options: new DashboardOptions
{
    DisplayNameFunc = (context, job) =>
        app.Services.GetRequiredService<IHangfireJobName>().GetName(job),
});

app.Run();
```

### Producer (enqueues events)

```csharp
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.DependencyInjection;
using Hangfire;
using YourProject.Events;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

// --- Hangfire configuration ---
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));

// --- ExecutionFlow configuration (no handlers needed on the producer) ---
builder.Services.AddHangfireToExecutionFlow();

var app = builder.Build();

// Enqueue events via IDispatcher
app.MapPost("/api/messages", (MessageRequest request, IDispatcher dispatcher) =>
{
    var @event = new SendMessageEvent
    {
        From = request.From,
        Content = request.Content,
        SentAt = DateTime.UtcNow
    };

    var jobId = dispatcher.Enqueue(@event);

    return Results.Ok(new { jobId, message = "Message enqueued successfully." });
});

app.Run();

record MessageRequest(string From, string Content);
```

### Marker Interface for Assembly Scanning

Create a marker interface in your handlers project so `Scan()` can locate the assembly:

```csharp
namespace YourProject.Handlers;

public interface IHandlerMark { }
```

Then use it: `options.Scan(typeof(IHandlerMark).Assembly);`

## Setup without Dependency Injection
```csharp
using ExecutionFlow.Hangfire;
using Hangfire;

// --- Hangfire configuration ---
GlobalConfiguration.Configuration
    .UseSqlServerStorage("Server=localhost;Database=HangfireDb;Trusted_Connection=True;");

// --- ExecutionFlow configuration ---
var setup = new HangfireSetup();
setup.Configure(options =>
    options.Scan(typeof(IHandlerMark).Assembly));
setup.ConfigureActivator().Build();

// --- Hangfire server ---
using var server = new BackgroundJobServer();

Console.WriteLine("Consumer started. Press Enter to exit.");
Console.ReadLine();
```

Key differences from the DI approach:
- Pure console app — no `Host`, `WebApplication`, or `IServiceCollection`
- Use `GlobalConfiguration.Configuration` directly for Hangfire storage
- Create `HangfireSetup` manually and call `Configure()` + `ConfigureActivator().Build()`
- Create `BackgroundJobServer` yourself

## Hangfire Dashboard

The dashboard displays handler-friendly names instead of raw method signatures. Configure the `DisplayNameFunc` to use ExecutionFlow's `IHangfireJobName`:

### With DI

```csharp
app.UseHangfireDashboard("/hangfire", options: new DashboardOptions
{
    DisplayNameFunc = (context, job) =>
        app.Services.GetRequiredService<IHangfireJobName>().GetName(job),
});
```

### Without DI

```csharp
// After HangfireSetup is configured:
var setup = new HangfireSetup();
setup.Configure(options => options.Scan(typeof(IHandlerMark).Assembly));
setup.ConfigureActivator().Build();

// Use setup directly — it implements IHangfireJobName
app.UseHangfireDashboard("/hangfire", options: new DashboardOptions
{
    DisplayNameFunc = (context, job) => setup.GetName(job),
});
```

> **Important:** The dashboard must run on a process where handlers are registered (via `AddHangfireToExecutionFlow` with `Scan`/`Add`, or via `HangfireSetup.Configure`). This is because `IHangfireJobName` relies on the handler registry to resolve display names. On a producer-only process without handler registration, names will fall back to raw method signatures.

## Custom ID (Job Tracking)

Custom IDs let you associate domain-meaningful identifiers with jobs for tracking, deduplication, or cancellation.

### Via `ICustomIdEvent`

Implement `ICustomIdEvent` on your event class. The custom ID is stored automatically when the event is enqueued:

```csharp
using ExecutionFlow.Abstractions;

namespace YourProject.Events;

public class ProcessOrderEvent : ICustomIdEvent
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public string GetCustomId() => $"order-{OrderId}";
}
```

### Via `context.SetCustomId()` inside a handler

You can also set the custom ID dynamically during handler execution:

```csharp
using ExecutionFlow.Abstractions;
using YourProject.Events;

namespace YourProject.Handlers;

public class ProcessOrderHandler : IHandler<ProcessOrderEvent>
{
    public Task HandleAsync(FlowContext<ProcessOrderEvent> context, CancellationToken cancellationToken)
    {
        context.SetCustomId($"order-{context.Event.OrderId}");
        context.Log.Info($"Processing order {context.Event.OrderId} for ${context.Event.Amount}");
        return Task.CompletedTask;
    }
}
```

### Querying Jobs by Custom ID

Use `IExecutionManager` (injected via DI or created manually) to query and manage jobs:

```csharp
app.MapGet("/api/orders/{orderId}/status", (string orderId, IExecutionManager manager) =>
{
    var customId = $"order-{orderId}";

    if (manager.IsRunning(customId))
        return Results.Ok(new { status = "processing" });

    if (manager.IsPending(customId))
        return Results.Ok(new { status = "enqueued" });

    return Results.Ok(new { status = "not found" });
});

app.MapDelete("/api/orders/{orderId}", (string orderId, IExecutionManager manager) =>
{
    manager.Cancel($"order-{orderId}");
    return Results.Ok(new { message = "Job cancelled." });
});
```

## Execution Manager

`IExecutionManager` provides methods to query and manage job state:

```csharp
public interface IExecutionManager
{
    bool IsRunning(string customId);                // is the job currently processing?
    bool IsPending(string customId);                // is the job enqueued and waiting?
    void Cancel(string customId);                   // delete/cancel the job
    IEnumerable<JobInfo> GetJobs(JobState state);   // list jobs by state
}
```

### JobInfo

Each `JobInfo` contains:

| Property | Type | Description |
|----------|------|-------------|
| `JobId` | `string` | Hangfire job ID |
| `CustomId` | `string` | Application-specific custom ID (if set) |
| `EventTypeName` | `string` | Type name of the event class |
| `EventType` | `Type` | Actual `Type` of the event |
| `State` | `JobState` | Current job state |
| `StateChangedAt` | `DateTimeOffset?` | When the state last changed |

### JobState

```csharp
public enum JobState
{
    Enqueued,
    Processing,
    Succeeded,
    Failed,
    Cancelled
}
```

### Listing Jobs

```csharp
app.MapGet("/api/jobs/failed", (IExecutionManager manager) =>
{
    var failedJobs = manager.GetJobs(JobState.Failed)
        .Select(j => new
        {
            j.JobId,
            j.CustomId,
            j.EventTypeName,
            j.State,
            j.StateChangedAt
        });

    return Results.Ok(failedJobs);
});
```

## Lifecycle Events (State Handlers)

ExecutionFlow fires lifecycle events when jobs transition between states. Implement any combination of the following interfaces:

| Interface | Fires When | Event Type |
|-----------|-----------|------------|
| `IOnEnqueued` | Job is added to the queue | `ExecutionEvent` |
| `IOnProcessing` | Job starts processing | `ExecutionEvent` |
| `IOnSucceeded` | Job completes successfully | `ExecutionSucceededEvent` (includes `Duration`) |
| `IOnFailed` | Job fails with an exception | `ExecutionFailedEvent` (includes `Exception`) |
| `IOnRetrying` | Job is being retried | `ExecutionRetryingEvent` (includes `AttemptNumber`) |
| `IOnCancelled` | Job is cancelled/deleted | `ExecutionEvent` |

### Creating a State Handler

```csharp
using ExecutionFlow.Abstractions.Events;

namespace YourProject.Handlers;

public class JobLifecycleLogger : IOnSucceeded, IOnFailed, IOnRetrying
{
    public void OnSucceeded(ExecutionSucceededEvent e)
    {
        Console.WriteLine($"[OK] Job {e.JobId} ({e.HandlerType.Name}) completed in {e.Duration.TotalSeconds:F1}s");
    }

    public void OnFailed(ExecutionFailedEvent e)
    {
        Console.WriteLine($"[FAIL] Job {e.JobId} ({e.HandlerType.Name}): {e.Exception.Message}");
    }

    public void OnRetrying(ExecutionRetryingEvent e)
    {
        Console.WriteLine($"[RETRY] Job {e.JobId} ({e.HandlerType.Name}) attempt #{e.AttemptNumber}");
    }
}
```

### Registering State Handlers

```csharp
// With DI
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
    options.AddStateHandler<JobLifecycleLogger>();
});

// Without DI
var setup = new HangfireSetup();
setup.Configure(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
    options.AddStateHandler<JobLifecycleLogger>();
});
```

### ExecutionEvent Properties

All lifecycle events share these base properties:

| Property | Type | Description |
|----------|------|-------------|
| `JobId` | `string` | Hangfire job ID |
| `CustomId` | `string` | Custom tracking ID (if set) |
| `HandlerType` | `Type` | The handler class that processed the job |

## Auto-Run Control

Control whether recurring jobs execute automatically or are registered only (useful for staging/debug environments).

### Global Setting

```csharp
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
    options.AutoRunRecurring = false; // register recurring jobs but don't execute them
});
```

### Per-Job Setting

```csharp
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(IHandlerMark).Assembly);
    options.AutoRunRecurring = true;                     // global: allow execution
    options.SetJobAutoRun<HeartbeatHandler>(false);       // disable only HeartbeatHandler
});
```

> Both `AutoRunRecurring` (global) and `SetJobAutoRun<T>()` (per-job) must be `true` for a recurring job to execute. If either is `false`, the job is blocked.

### Orphan Job Cleanup

When handlers are removed from the codebase, their recurring jobs remain in Hangfire storage. Enable cleanup:

```csharp
options.RemoveOrphanRecurringJobs = true;
```

This removes any recurring job from Hangfire that is no longer registered in ExecutionFlow.

## Progress Bars (Hangfire.Console)

Display progress bars on the Hangfire dashboard using the `ExecutionFlow.Hangfire.Console` package.

```shell
dotnet add package ExecutionFlow.Hangfire.Console
```

### Usage in a Handler

```csharp
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Console;

namespace YourProject.Handlers;

public class DataImportHandler : IHandler<DataImportEvent>
{
    public async Task HandleAsync(FlowContext<DataImportEvent> context, CancellationToken cancellationToken)
    {
        var items = context.Event.Items;
        var progressBar = context.CreateProgressBar("Importing data");

        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessItem(items[i]);
            progressBar.SetValue(i + 1, items.Count); // updates percentage automatically
        }

        progressBar.Complete(); // sets to 100%
        context.Log.Success($"Imported {items.Count} items.");
    }

    private Task ProcessItem(object item) => Task.CompletedTask;
}
```

### ProgressBar API

| Method | Description |
|--------|-------------|
| `context.CreateProgressBar()` | Create a progress bar (no title) |
| `context.CreateProgressBar("title")` | Create a progress bar with a title |
| `SetValue(float percentage)` | Set progress by percentage (0–100) |
| `SetValue(int current, int total)` | Set progress by item count (auto-calculates percentage) |
| `Complete()` | Set progress to 100% |

> **Note:** Progress bars require `Hangfire.Console` to be configured in your Hangfire setup: `config.UseConsole()`.

## Handler Registration

### Assembly Scanning (recommended)

Auto-discover all `IHandler` and `IHandler<TEvent>` implementations in an assembly:

```csharp
options.Scan(typeof(IHandlerMark).Assembly);
```

The scanner:
- Finds all concrete classes implementing `IHandler` or `IHandler<TEvent>`
- Reads `[Recurring("cron")]` attributes for recurring job schedules
- Reads `[DisplayName("name")]` attributes for dashboard display names
- Ignores abstract classes and interfaces

### Manual Registration

Register individual handler types explicitly:

```csharp
options.Add(typeof(SendMessageHandler));
options.Add(typeof(HeartbeatHandler));
```

### Combining Both

```csharp
options.Scan(typeof(IHandlerMark).Assembly);
options.Add(typeof(SomeOtherHandler)); // from a different assembly
```

## API Reference

### Core Abstractions (`ExecutionFlow`)

| Type | Description |
|------|-------------|
| `IHandler<TEvent>` | Async event handler — `HandleAsync(FlowContext<TEvent>, CancellationToken)` |
| `IHandler` | Async recurring handler — `HandleAsync(FlowContext, CancellationToken)` |
| `IDispatcher` | Enqueue events — `Enqueue<TEvent>(TEvent): string` |
| `IExecutionManager` | Query/cancel jobs — `IsRunning`, `IsPending`, `Cancel`, `GetJobs` |
| `IExecutionLogger` | Logging — `Info`, `Success`, `Warning`, `Error` |
| `ICustomIdEvent` | Custom ID on events — `GetCustomId(): string` |
| `FlowContext<TEvent>` | Execution context with `Event`, `Log`, `Items`, `SetCustomId` |
| `FlowContext` | Base execution context with `Log`, `Items` |
| `JobInfo` | Job metadata — `JobId`, `CustomId`, `EventTypeName`, `EventType`, `State`, `StateChangedAt` |
| `JobState` | Enum — `Enqueued`, `Processing`, `Succeeded`, `Failed`, `Cancelled` |
| `[Recurring("cron")]` | Attribute to mark a handler as recurring with a cron schedule |

### Lifecycle Events (`ExecutionFlow.Abstractions.Events`)

| Type | Description |
|------|-------------|
| `IOnEnqueued` | `void OnEnqueued(ExecutionEvent)` |
| `IOnProcessing` | `void OnProcessing(ExecutionEvent)` |
| `IOnSucceeded` | `void OnSucceeded(ExecutionSucceededEvent)` — includes `Duration` |
| `IOnFailed` | `void OnFailed(ExecutionFailedEvent)` — includes `Exception` |
| `IOnRetrying` | `void OnRetrying(ExecutionRetryingEvent)` — includes `AttemptNumber` |
| `IOnCancelled` | `void OnCancelled(ExecutionEvent)` |

### Hangfire Integration (`ExecutionFlow.Hangfire`)

| Type | Description |
|------|-------------|
| `HangfireSetup` | Manual setup — `Configure()`, `ConfigureActivator()`, `Build()`, `GetName(Job)` |
| `HangfireOptions` | Configuration — `AutoRunRecurring`, `RemoveOrphanRecurringJobs`, `AddStateHandler<T>()`, `SetJobAutoRun<T>()` |
| `IHangfireJobName` | Dashboard name resolver — `GetName(Job): string` |

### Dependency Injection (`ExecutionFlow.Hangfire.DependencyInjection`)

| Type | Description |
|------|-------------|
| `AddHangfireToExecutionFlow()` | Extension on `IServiceCollection` — registers handlers, dispatcher, and execution manager |

### Console (`ExecutionFlow.Hangfire.Console`)

| Type | Description |
|------|-------------|
| `context.CreateProgressBar()` | Extension on `FlowContext` — creates a dashboard progress bar |
| `ExecutionProgressBar` | Progress bar — `SetValue(float)`, `SetValue(int, int)`, `Complete()` |
