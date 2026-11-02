# Implementation Plan: Local hosting

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/local-hosting/plan.md` (status: draft, v1.3.0). Decisions are in [research.md](research.md).

## Summary

A new provider package, `ExecutionFlow.Hosting`, runs recurring handlers inside the .NET Generic Host: one scheduler per enabled handler, an in-memory dependency gate by generation, a concurrency limiter, a run-once mode and a startup report. The core keeps declaration and validation (`ExecutionPlanner`/`ExecutionPlan`).

## Technical Context

**Language/Version**: C# on `netstandard2.0;net8.0`; tests on .NET 10

**Primary Dependencies**: `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0), `Cronos`

**Storage**: none (generations in memory, RN-003)

**Testing**: xUnit + NSubstitute, `FakeTimeProvider` on the `net8.0` asset

**Target Platform**: Generic Host apps (Windows service, console app)

**Project Type**: library

**Performance Goals**: a waiting dependent holds no thread (SC-001)

**Constraints**: `netstandard2.0` build stays functional (SC-002); not trimming-safe (R-003)

**Scale/Scope**: one process, tens of handlers

## Constitution Check

- I. Dependency-free core: ✅ the core keeps only declaration and validation; multi-targeting follows principle I (newer TFM behind `#if`).
- II. Processors isolated: ✅ only `ExecutionFlow.Hosting` references the Generic Host and Cronos.
- III. Optional integrations are separate packages: ✅ opt-in package.
- V. Minimal public surface: ✅ gate, limiter, executor and hosted services are internal.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/011-local-hosting/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow.Hosting` (new) | `HostingOptions`, `ServiceCollectionExtensions.AddExecutionFlowHosting`, `IExecutionPlanRunner`, `RunOnceResult`, `HandlerRunOutcome`, `HandlerRunStatus` | New package |

```text
Src/ExecutionFlow.Hosting/
Tests/   # Hosting tests (DependencyGateTests, RecurringHandlerHostTests, ExecutionPlanRunnerTests, ...)
```

## Design

- **Target frameworks:** `netstandard2.0;net8.0`.
  - On `net8.0`, all time comes from `TimeProvider` (`GetUtcNow`, `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, `Task.WaitAsync(TimeSpan, TimeProvider, CancellationToken)`).
  - On `netstandard2.0`, an internal `SystemClock` polyfill wraps `DateTimeOffset.UtcNow` and `Task.Delay`, and implements the timeout wait with `Task.WhenAny`.
  - Both targets go through one internal `IClock` seam, and `#if NET8_0_OR_GREATER` picks the implementation.
- **Dependencies:** `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0), and `Cronos` for cron.
- **`DependencyGate`** (singleton):
  - one `Gate` per handler `Type`, holding a `long` generation and a `TaskCompletionSource` that is swapped on every `Signal`;
  - the generation and the task are read together under the gate's lock;
  - the old TCS is completed outside the lock, and is created with `RunContinuationsAsynchronously`.
- **`RecurringHandlerHost<THandler>`** (`BackgroundService`, one per enabled handler, registered as `IHostedService` through a factory, with the handler type closed by `MakeGenericType`), in a loop:
  1. compute the next occurrence;
  2. delay until then;
  3. wait on the gate for every prerequisite, and remember the consumed generations;
  4. take the limiter;
  5. run inside a new scope;
  6. release the limiter;
  7. on success, `Signal`.
- **`HandlerExecutor`**: shared by the scheduler and the runner. It opens the scope, resolves the handler, builds the `FlowContext` with `FlowContextBuilder` + `ExecutionLoggerFactory`, calls `HandleAsync` and disposes the context.
- **`ExecutionPlanRunner`**: runs `ExecutionOrder` sequentially, keeping the set of handlers that completed.
- **`RunOnceHostedService`**: runs from `StartAsync` on a background task, so the host startup isn't blocked, then sets `Environment.ExitCode` and calls `StopApplication`.
- **Schedule:**
  - cron is parsed by `Cronos`, in 5-field format, or 6-field when the expression has 6 fields;
  - the next occurrence comes from `GetNextOccurrence(now, timeZone)`;
  - an interval schedule's next occurrence is `now + interval`.

## Complexity Tracking

No constitution violations. (Multi-targeting `net8.0` was an amendment under the old constitution, ADR-0008; it is now allowed by principle I.)
