# ExecutionFlow.Hangfire.DependencyInjection

ASP.NET Core dependency injection extensions for the [ExecutionFlow](https://www.nuget.org/packages/ExecutionFlow) framework.

## Installation

```bash
dotnet add package ExecutionFlow.Hangfire.DependencyInjection
```

## Setup

### Full setup (consumer + producer)

```csharp
builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(typeof(SendEmailHandler).Assembly);
});
```

### Producer-only (isolated dispatcher)

Publish jobs to a separate database without affecting an existing Hangfire instance:

```csharp
var separateStorage = new SqlServerStorage("Server=...;Database=SeparateDb;...");
builder.Services.AddExecutionFlowDispatcher(_ => separateStorage);

// Or resolve storage from DI
builder.Services.AddExecutionFlowDispatcher();
```

## Configuration

```csharp
builder.Services.AddHangfireToExecutionFlow(options =>
{
    options.Scan(assembly, type => type.Namespace.StartsWith("MyApp.Handlers"));
    options.GlobalRecurringAutoRun = true;
    options.RemoveOrphanRecurringJobs = true;
    options.DisableRecurringRetries = true;
    options.DeduplicationBehavior = DeduplicationBehavior.SkipIfExists;
    options.AddStateHandler<JobMonitor>();
});
```

| Option | Default | Description |
|---|---|---|
| `GlobalRecurringAutoRun` | `true` | Auto-start recurring jobs |
| `RemoveOrphanRecurringJobs` | `false` | Delete recurring jobs not in code |
| `DisableRecurringRetries` | `true` | No retries for recurring jobs |
| `DeduplicationBehavior` | `Disabled` | Duplicate job handling strategy |

## Related Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core abstractions |
| **ExecutionFlow.Hangfire** | Hangfire integration |
| **ExecutionFlow.Hangfire.Console** | Console logging + progress bars |
