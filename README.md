# ExecutionFlow

A .NET library for defining, dispatching, and managing background jobs on top of [Hangfire](https://www.hangfire.io/) with typed handler contracts, automatic discovery, and lifecycle hooks.

## Packages

| Package | Description |
|---------|-------------|
| **ExecutionFlow** | Core contracts and abstractions — no external dependencies. |
| **ExecutionFlow.Hangfire** | Hangfire provider — handler registration, dispatching, state filters, and execution manager. |
| **ExecutionFlow.Hangfire.Console** | Optional progress-bar reporting on the Hangfire dashboard. |

## Getting Started

Install the packages:

```shell
dotnet add package ExecutionFlow
dotnet add package ExecutionFlow.Hangfire
```

Configure and create your first handler:

```csharp
using System.Reflection;
using System.Threading;
using ExecutionFlow;
using ExecutionFlow.Hangfire;
using ExecutionFlow.Abstractions;
using Hangfire;

// 1. Configure Hangfire (your responsibility)
GlobalConfiguration.Configuration
    .UseSqlServerStorage("your-connection-string");

// 2. Register handlers
ExecutionFlowSetup.Configure(options =>
    options.Scan(Assembly.GetExecutingAssembly()));

// 3. Wire up the Hangfire provider
HangfireSetup.Configure(options => { });

// A recurring handler — runs on a cron schedule
[Recurring("*/5 * * * *")]
public class CleanupHandler : IHandler
{
    public void Execute(ExecutionContext context, CancellationToken ct)
    {
        context.Log.Info("Running cleanup...");
    }
}
```

## Documentation

Full documentation is available in the [`docs/`](docs/) directory:

- [Introduction](docs/introduction.md) — what ExecutionFlow is and how it relates to Hangfire
- [Installation and Configuration](docs/installation.md) — step-by-step setup guide
- [Handlers](docs/handlers.md) — recurring and event-driven handlers
- [Custom Id](docs/custom-id.md) — domain-meaningful job identifiers
- [Execution Manager](docs/execution-manager.md) — query and cancel jobs
- [Lifecycle Events](docs/lifecycle-events.md) — hook into job state transitions
- [Progress Bars](docs/progress-bar.md) — dashboard progress reporting
- [Quick Reference](docs/reference.md) — all public API surfaces at a glance
