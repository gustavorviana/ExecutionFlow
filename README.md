# ExecutionFlow

[![CI](https://github.com/gustavorviana/ExecutionFlow/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/gustavorviana/ExecutionFlow/actions/workflows/ci.yml?query=branch%3Amain)
[![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.svg)](https://www.nuget.org/packages/ExecutionFlow)
[![License](https://img.shields.io/github/license/gustavorviana/ExecutionFlow)](https://github.com/gustavorviana/ExecutionFlow/blob/main/LICENSE.txt)

A lightweight .NET foundation for background work: write handlers once, run them on any execution provider.

You write handlers, schedules and execution plans against a **dependency-free core**. A **provider** runs them. Two providers exist today, and more can be added without changing your handlers:

- **[Hangfire](https://www.hangfire.io/)** (`ExecutionFlow.Hangfire`): persistent queues, retries, distributed workers and the dashboard.
- **Local host** (`ExecutionFlow.Hosting`): recurring handlers inside the .NET Generic Host, without storage.

## Features

- **Handlers** for events (`IHandler<TEvent>`) and recurring work (`IHandler`). [Handlers](https://github.com/gustavorviana/ExecutionFlow/wiki/Handlers)
- **Schedules** with cron or intervals down to one second, time zones, stable IDs and overlap control. [Recurring Jobs](https://github.com/gustavorviana/ExecutionFlow/wiki/Recurring-Jobs)
- **Execution plans**: recurring handlers that wait for others, validated at startup. [Execution Plan](https://github.com/gustavorviana/ExecutionFlow/wiki/Execution-Plan)
- **Dispatching** with custom IDs and deduplication. [Dispatching Events](https://github.com/gustavorviana/ExecutionFlow/wiki/Dispatching-Events)
- **Lifecycle hooks** and an **execution manager** to react to and manage jobs. [Lifecycle Hooks](https://github.com/gustavorviana/ExecutionFlow/wiki/Lifecycle-Hooks), [Execution Manager](https://github.com/gustavorviana/ExecutionFlow/wiki/Execution-Manager)
- **Logging** with structured templates, to `ILogger` or your own loggers. [Logging](https://github.com/gustavorviana/ExecutionFlow/wiki/Logging)
- **Testable handlers**: unit test them with nothing but the core. [Testing Handlers](https://github.com/gustavorviana/ExecutionFlow/wiki/Testing-Handlers)

Not every provider supports every feature: see [Provider support](https://github.com/gustavorviana/ExecutionFlow/wiki/Architecture-and-Providers#provider-support).

## Packages

| Package | Layer | Version | Description |
|---------|-------|---------|-------------|
| **ExecutionFlow** | Core | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.svg)](https://www.nuget.org/packages/ExecutionFlow) | Handlers, schedules, execution plan and contracts. No external dependencies |
| **ExecutionFlow.Extensions.Logging** | Core bridge | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Extensions.Logging.svg)](https://www.nuget.org/packages/ExecutionFlow.Extensions.Logging) | Sends `context.Log` to `ILogger`, with any provider |
| **ExecutionFlow.Hangfire** | Hangfire provider | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire) | Dispatching, filters, execution manager |
| **ExecutionFlow.Hangfire.DependencyInjection** | Hangfire provider | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.DependencyInjection.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire.DependencyInjection) | Microsoft DI registration |
| **ExecutionFlow.Hangfire.Console** | Hangfire provider | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hangfire.Console.svg)](https://www.nuget.org/packages/ExecutionFlow.Hangfire.Console) | Dashboard console logging and progress bars |
| **ExecutionFlow.Hosting** | Local host provider | [![NuGet](https://img.shields.io/nuget/v/ExecutionFlow.Hosting.svg)](https://www.nuget.org/packages/ExecutionFlow.Hosting) | Recurring handlers in the .NET Generic Host |

## Quick start

Write a handler. It only depends on the core:

```csharp
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

[Recurring("*/5 * * * *")]   // every 5 minutes
public class SyncHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken ct)
    {
        context.Log.Info("Syncing, attempt {Attempt}", context.AttemptNumber);
        return Task.CompletedTask;
    }
}
```

Then pick a provider. **Hangfire:**

```bash
dotnet add package ExecutionFlow.Hangfire.DependencyInjection
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer
```

```csharp
builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddHangfireToExecutionFlow(options => options.Scan(typeof(SyncHandler).Assembly));
```

**Or the local host**, with the same handler and no storage:

```bash
dotnet add package ExecutionFlow.Hosting
```

```csharp
builder.Services.AddExecutionFlowHosting(options => options.Scan(typeof(SyncHandler).Assembly));
```

Publishing events, execution plans and everything else is in the [Getting Started](https://github.com/gustavorviana/ExecutionFlow/wiki/Getting-Started) tutorial.

## Documentation

The documentation lives in the [wiki](https://github.com/gustavorviana/ExecutionFlow/wiki):

- [Getting Started](https://github.com/gustavorviana/ExecutionFlow/wiki/Getting-Started): first jobs in a few minutes
- [Architecture and Providers](https://github.com/gustavorviana/ExecutionFlow/wiki/Architecture-and-Providers): the core, the providers and what each one supports
- [Migrating from 1.x to 2.0](https://github.com/gustavorviana/ExecutionFlow/wiki/Migrating-from-1.x-to-2.0): 2.0 changes documented behavior; read this before upgrading

Runnable samples are in [Examples/](Examples): producer, consumer (with and without DI), local host, handler configuration and .NET Aspire.

## Supported platforms

The libraries target **.NET Standard 2.0**, so they run on .NET Framework 4.6.1+ and .NET Core / .NET 5+. `ExecutionFlow.Hosting` also targets **.NET 8**. The Hangfire packages require Hangfire 1.8.

## Contributing

The project follows Spec-Driven Development with [GitHub Spec Kit](https://github.com/github/spec-kit): every feature has a spec in [specs/](specs), and behavior changes start there. Read the [constitution](.specify/memory/constitution.md) before opening a pull request.

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## License

[MIT](LICENSE.txt)
