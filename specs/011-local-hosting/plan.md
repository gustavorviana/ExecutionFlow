# Implementation Plan: Local hosting

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-09-30, revised 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/local-hosting/plan.md`, revised after `/speckit-analyze` (findings I1, I2, U1, U2, F1, D1, D2, C1, C2, A1). Decisions in [research.md](research.md).

## Summary

A new provider package, `ExecutionFlow.Hosting`, runs recurring handlers inside the .NET Generic Host with the same meaning as Hangfire: per-handler schedulers (cron or interval), the execution plan's generation rule and run modes in memory, Hangfire-mirrored overlap control, a concurrency limiter, a run-once mode and a startup report. Declarations come from the core ([012](../012-interval-and-overlap/spec.md)).

## Technical Context

**Language/Version**: C# on `netstandard2.0;net8.0`; tests on .NET 10 (`net8.0` asset) plus a `netstandard2.0`-asset test run for the system clock

**Primary Dependencies**: `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0), `Cronos`; tests: `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`)

**Storage**: none (generations in memory, RN-003)

**Testing**: xUnit + NSubstitute, new project `Tests/ExecutionFlow.Hosting.Tests`

**Target Platform**: Generic Host apps (Windows service, console app)

**Project Type**: library

**Performance Goals**: waiting runs hold no thread (SC-001, measured by `RecurringHandlerHostTests.WaitingRuns_HoldNoThreads`)

**Constraints**: `netstandard2.0` build stays functional and tested (SC-002); not trimming-safe (research R-001)

**Scale/Scope**: one process, tens of handlers

## Constitution Check

- I. Dependency-free core: ✅ the core only declares (012); the package multi-targets per principle I.
- II. Processors isolated: ✅ only `ExecutionFlow.Hosting` references the Generic Host and Cronos.
- III. Optional integrations are separate packages: ✅ opt-in package.
- V. Minimal public surface: ✅ gate, limiter, executor, clock and hosted services are internal.
- VII. Tested, traceable behavior: ✅ every AC (including edge cases) names a test; the `netstandard2.0` clock has its own test.

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
| `ExecutionFlow.Hosting` (new) | `HostingOptions` (`SetJobAutoRun`, `GlobalRecurringAutoRun`, `MaxConcurrency`, `RunOnce`, `RecurringTimeZone`, `TimeProvider`, `IntervalFromEnd`/`SetIntervalFromEnd`, `SkipWhenOverlapping`/`SetSkipWhenOverlapping`), `ServiceCollectionExtensions.AddExecutionFlowHosting`, `IExecutionPlanRunner`, `RunOnceResult`, `HandlerRunOutcome`, `HandlerRunStatus` | New package |

```text
Src/ExecutionFlow.Hosting/            # delete the stale bin/ and obj/ left there first
├── ExecutionFlow.Hosting.csproj      # netstandard2.0;net8.0
├── HostingOptions.cs, ServiceCollectionExtensions.cs
├── Abstractions/IExecutionPlanRunner.cs, RunOnceResult.cs, HandlerRunOutcome.cs, HandlerRunStatus.cs
└── Infrastructure/IClock.cs, SystemClock.cs, TimeProviderClock.cs, DependencyGate.cs, ConcurrencyLimiter.cs,
    OverlapGuard.cs, HandlerExecutor.cs, RecurringHandlerHost.cs, ExecutionPlanRunner.cs,
    RunOnceHostedService.cs, StartupReportHostedService.cs
Tests/ExecutionFlow.Hosting.Tests/    # net10.0 (net8.0 asset) + a netstandard2.0-asset run for SystemClockTests
```

## Design

- **Target frameworks:** `netstandard2.0;net8.0`. One internal `IClock` seam (`UtcNow`, `Delay`, `WaitAsync(task, timeout)`): `TimeProviderClock` on `net8.0`, `SystemClock` (`DateTimeOffset.UtcNow`, `Task.Delay`, `Task.WhenAny`) on `netstandard2.0`, picked with `#if NET8_0_OR_GREATER`.
- **Enabled** (FR-002): plan handler → `step.Enabled`; others → `SetJobAutoRun<T>` > `GlobalRecurringAutoRun`. `HostingOptions` overrides `OnPlanRegistering` (012 FR-004) with the same conflict check as Hangfire.
- **`DependencyGate`** (singleton, in memory): per handler a `long` generation; per dependent the consumed generations. `Complete(handler)` increments the generation under one lock and returns the dependents that became ready (all prerequisites newer than consumed, not running, enabled), marking their generations as consumed atomically, so concurrent completions trigger each dependent once (AC-005.8). `IsReady(dependent)` serves `RunOnOwnSchedule`.
- **`RecurringHandlerHost`** (`BackgroundService`, one per handler that runs on its own schedule): loop — next occurrence (cron via Cronos in the resolved time zone; interval from host start, or from the previous end when `IntervalFromEnd`) → `IClock.Delay` → for `RunOnOwnSchedule`, check the gate and skip with a log if not ready → run through `OverlapGuard` and the limiter (fire-and-forget per occurrence so fixed cadence can overlap).
- **Dependents in default/`MinInterval` mode** have no host loop: when `Complete` returns them, a trigger task runs them; `MinInterval` delays the trigger until `lastEnd + interval` (cancellable by host stop). A running dependent isn't returned by `Complete`; when it finishes, it asks the gate again (AC-005.7).
- **`OverlapGuard`** (per handler `SemaphoreSlim(1)` when overlap is off): wait mode → `WaitAsync(timeout)`, timeout → the run fails with an error log; skip mode → `Wait(0)`, busy → skip with a log. Overlap on → no guard.
- **`ConcurrencyLimiter`**: `SemaphoreSlim(MaxConcurrency)` taken with `WaitAsync` after the overlap guard and the gate, so nothing waiting holds a thread (SC-001).
- **`HandlerExecutor`**: shared by hosts, triggers and the runner. New scope, resolves the handler, builds the `FlowContext` with `FlowContextBuilder` + `ExecutionLoggerFactory`, calls `HandleAsync`, disposes; on success calls `DependencyGate.Complete`. Cancellation by host stop ends quietly with no completion (AC-004.3).
- **`ExecutionPlanRunner`**: runs `plan.Steps` in order, then the enabled handlers outside the plan in registration order (a handler also in the plan runs only in its plan position); keeps the set of completed handlers to skip dependents.
- **`RunOnceHostedService`**: runs from `StartAsync` on a background task, sets `Environment.ExitCode`, calls `StopApplication`.

## Complexity Tracking

No constitution violations.
