# Implementation Plan: Lifecycle hooks

**Branch**: N/A (migrated) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/lifecycle-hooks/plan.md` (v1.2.0, status: approved; the spec's "Decided to-be changes", items 1–7). Decisions and findings are in [research.md](research.md).

## Summary

Fixes `Duration` (F-001), fires `OnFailed` only on the final failure (F-002), isolates hook exceptions (F-003), and moves `Duration` to the events where it means something (F-006). It also documents where hooks run and what scheduled jobs fire.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: `Hangfire.Core` 1.8.23 (in `ExecutionFlow.Hangfire` only)

**Storage**: N/A (reads the Processing state's `StartedAt` and job parameters)

**Testing**: xUnit + NSubstitute (`StateFilterTests` run the real `AutomaticRetryAttribute` first; `ExecutionEventTests`; `HangfireSetupGlobalFiltersTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: hooks run synchronously in the state transition; they should be fast (RN-005)

**Constraints**: a hook failure costs the job nothing but a log entry (SC-001)

**Scale/Scope**: a few hooks per consumer

## Constitution Check

- I. Dependency-free core: ✅ the core only gains event properties and `HookErrorContext`.
- II. Processors isolated: ✅ the state filter and `HookErrorHandler` live in `ExecutionFlow.Hangfire`.
- IV. Producer-only mode mutates no global state: ✅ producer-only hosts register no filters (RN-001).
- V. Minimal public surface: ⚠️ source break on `ExecutionEvent.Duration`, accepted in 1.2.0 with the other 1.2.0 breaks.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/006-lifecycle-hooks/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `ExecutionEvent.Duration` and its ctor parameter removed. `Duration` added to `ExecutionSucceededEvent`, `ExecutionFailedEvent`, `ExecutionRetryingEvent`. `ExecutionRetryingEvent.Exception` added. New `HookErrorContext` | **Yes (source)**: `Duration` on a base `ExecutionEvent`, and the `ExecutionEvent` ctor's `duration` argument |
| `ExecutionFlow.Hangfire` | `HangfireStateFilter` registered with order 100. New `HangfireOptions.HookErrorHandler` | Behavior: `OnFailed` only on the final failure |

```text
Src/ExecutionFlow/Abstractions/          (ExecutionEvent and subtypes, IOn* interfaces, HookErrorContext)
Src/ExecutionFlow.Hangfire/Infrastructure/Filters/HangfireStateFilter.cs
Src/ExecutionFlow.Hangfire/HangfireOptions.cs
Tests/ExecutionFlow.Tests/, Tests/ExecutionFlow.Hangfire.Tests/
```

## Design

### Spike result (2026-09-30)

Running Hangfire 1.8.23's `AutomaticRetryAttribute.OnStateElection` on a real `ElectStateContext` with a candidate `FailedState(new InvalidOperationException("boom"))`:
- `Attempts = 3` → candidate becomes `Scheduled` ("Retry attempt 1 of 3: boom"), and `TraversedStates` holds the original `FailedState` **with its exception**.
- `Attempts = 0` → the candidate stays `Failed`.

So a filter that runs **after** the retry filter sees the final decision and still has the exception.

### Design

- **Order:** `GlobalJobFilters.Filters.Add(stateFilter, 100)`. That's after `AutomaticRetryAttribute` (20) and `StatisticsHistoryAttribute` (30), and before `ContinuationsSupportAttribute` (1000). Handler-level `[AutomaticRetry]` (order 20) also runs first.
- **Mapping** (on the candidate state after the retry decision):
  - Failed → `OnFailed` (final).
  - Deleted with a `FailedState` in `TraversedStates` (the retry filter gave up with `OnAttemptsExceeded = Delete`) → `OnFailed` (final), not `OnCancelled`.
  - Deleted otherwise → `OnCancelled`.
  - Scheduled with a `FailedState` in `TraversedStates` → `OnRetrying`, with that exception and `AttemptNumber` = the `RetryCount` job parameter.
  - Enqueued from Failed (a manual requeue via `Retry` or the dashboard) → `OnRetrying`, with no exception.
  - Enqueued from Scheduled with retry count > 0 (a retry becoming due) → **no hook**, because `OnRetrying` already fired when it was scheduled. So each retry fires exactly once.
  - Enqueued otherwise → `OnEnqueued`. Processing → `OnProcessing`. Succeeded → `OnSucceeded`.
- **Duration:** parse `StartedAt` with `DateTimeStyles.AdjustToUniversal | AssumeUniversal` and the invariant culture. It's only computed for Succeeded, Failed and Retrying.
- **Isolation:** each hook is resolved and invoked in its own try/catch. On an exception, `HookErrorHandler` is called if set, otherwise `Trace.TraceWarning`. An exception thrown by `HookErrorHandler` itself is swallowed and traced.

### Versioning impact

1.2.0 (source break on `Duration`, accepted as with the other 1.2.0 breaks).

### Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Users relying on `OnFailed` for every attempt | They miss intermediate failures | `OnRetrying` now carries the exception. Release notes |
| Hooks that threw on purpose to affect the job | They no longer affect it | Documented: use a Hangfire filter |

## Complexity Tracking

No constitution violations. The source break on `Duration` was accepted in 1.2.0 (principle V).
