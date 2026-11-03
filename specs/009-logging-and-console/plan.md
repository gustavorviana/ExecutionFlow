# Implementation Plan: Logging and console

**Branch**: `main` (brownfield, v1.2.0) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/logging-and-console/plan.md` (status: approved). Implements the spec's "Decided to-be changes", items 1–7. Decisions are in [research.md](research.md).

## Summary

Isolate each execution logger so logging never fails a job, dedupe logger factories, give factories optional job and handler facts, make the Hangfire console format like `ILogger` templates with a minimum level, and add the opt-in `ExecutionFlow.Extensions.Logging` bridge to `ILogger`.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core none; `Hangfire.Console` 1.4.3 (console package); `Microsoft.Extensions.Logging.Abstractions` >= 6.0.0 (new bridge package)

**Storage**: N/A

**Testing**: xUnit + NSubstitute (`ExecutionLoggerIsolationTests`, `ConsoleFormattingTests`, `MicrosoftExecutionLoggerFactoryTests`, ...)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: N/A

**Constraints**: no breaking change to `IExecutionLoggerFactory` (R-001)

**Scale/Scope**: one logger per factory per execution

## Constitution Check

- I. Dependency-free core: ✅ the core changes add no dependency; the `ILogger` bridge is its own package.
- II. Processors isolated: ✅ the bridge depends only on the core, so any processor can use it.
- III. Optional integrations are separate packages: ✅ `ExecutionFlow.Extensions.Logging` is opt-in.
- V. Minimal public surface: ✅ `LogMessageTemplate` is internal; new public types are additive.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/009-logging-and-console/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | Composite isolation. `AddLogger` dedupe. New `ExecutionLoggerContext`, `IExecutionLoggerContextFactory`, `FlowContextBuilder.SetHandler(Type, Type)`, `ExecutionLoggerFactory.CreateLogger(ExecutionLoggerContext)` | No |
| `ExecutionFlow.Hangfire` | `HangfireJobDispatcher` calls `SetHandler` | No |
| `ExecutionFlow.Hangfire.Console` | `ILogger`-style templates, `ConsoleConfig.MinimumLevel`, `CreateProgressBar` uses `GetPerformContext()` | Behavior: named placeholders are now filled |
| `ExecutionFlow.Extensions.Logging` (new) | `options.AddMicrosoftLogging()`, `MicrosoftExecutionLoggerFactory` | New package |

```text
Src/ExecutionFlow/                      # CompositeExecutionLogger, ExecutionLoggerFactory, ExecutionLoggerContext, FlowContextBuilder
Src/ExecutionFlow.Hangfire/             # HangfireJobDispatcher
Src/ExecutionFlow.Hangfire.Console/     # ConsoleConfig, HangfireExecutionLogger, LogMessageTemplate, progress bars
Src/ExecutionFlow.Extensions.Logging/   # MicrosoftExecutionLoggerFactory, AddMicrosoftLogging
Tests/ExecutionFlow.Tests/
Tests/ExecutionFlow.Hangfire.Tests/     # console tests
Tests/ExecutionFlow.Extensions.Logging.Tests/
```

## Design

- **Isolation:** `CompositeExecutionLogger.Log` wraps each logger in try/catch → `Trace.TraceWarning`.
- **Dedupe:** `ExecutionFlowOptions.AddLogger(Type)` ignores a type that's already registered.
- **Logger context (optional interface, no break):** `IExecutionLoggerContextFactory : IExecutionLoggerFactory` adds `CreateLogger(ExecutionLoggerContext)`. `ExecutionLoggerFactory.CreateLogger(ExecutionLoggerContext)` uses it when a factory implements it, otherwise the `FlowParameters` overload. `FlowContextBuilder` builds the context from `SetJob` and the new `SetHandler(handlerType, eventType)`. `HangfireJobDispatcher` sets both.
- **Templates:** `LogMessageTemplate.Format(message, args)` (internal to the console package) fills each `{placeholder}` (named or numeric, with optional `,alignment` and `:format`) with the argument **in order of appearance**, like `Microsoft.Extensions.Logging`. `{{`/`}}` are literal braces. With no arguments the message is written as-is. Placeholders without an argument stay as written.
- **Minimum level:** `ConsoleConfig.MinimumLevel` (default `Trace`). `Success` is compared as `Information`.
- **`ExecutionFlow.Extensions.Logging`** (netstandard2.0, depends on `ExecutionFlow` + `Microsoft.Extensions.Logging.Abstractions` >= 6.0.0):
  - `MicrosoftExecutionLoggerFactory : IExecutionLoggerContextFactory`, built with `ILoggerFactory`. The category is the handler type's full name, otherwise the event type's, otherwise `"ExecutionFlow"`.
  - The logger calls `ILogger.Log(level, message, args)` inside `BeginScope({ JobId, AttemptNumber })`, so templates stay structured.
  - Level mapping: Trace/Debug/Information/Warning/Error/Critical 1:1, and `Success` → `Information`.
  - `options.AddMicrosoftLogging()` = `AddLogger<MicrosoftExecutionLoggerFactory>()`. `ILoggerFactory` is resolved from the container (DI), or must be registered in the built-in activator.

### Versioning impact

1.2.0 (lockstep, see [constitution](../../.specify/memory/constitution.md) "Versioning and Release"). The new package ships at 1.2.0.

## Complexity Tracking

No constitution violations.
