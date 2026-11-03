# Implementation Plan: Execution manager

**Branch**: N/A (migrated) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/execution-manager/plan.md` (status: approved; it covered the spec's to-be changes, items 1–7). Decisions and findings are in [research.md](research.md).

## Summary

Let `IExecutionManager` cancel Scheduled jobs (an exact Hangfire ID is deleted without a scan) and report whether a cancel worked, list jobs lazily and only for ExecutionFlow, expose `HandlerType` for recurring jobs, add `JobState.Scheduled`, and read timestamps as UTC.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: Hangfire (only in `ExecutionFlow.Hangfire`)

**Storage**: Hangfire job storage (monitoring API)

**Testing**: xUnit + NSubstitute (`ExecutionManagerTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: bounded `GetJobs` memory (SC-001); O(1) cancel by exact ID or reservation key (SC-002)

**Constraints**: keep the `GetJobs` signature (no paged overload, R-001)

**Scale/Scope**: thousands of jobs per state in a busy app

## Constitution Check

- I. Dependency-free core: ✅ the core only gains contract members.
- II. Processors isolated: ✅ Hangfire logic stays in `HangfireExecutionManager`.
- V. Minimal public surface: ⚠️ breaking for `IExecutionManager` implementers, accepted for 1.2.0 (see Versioning and Release in the [constitution](../../.specify/memory/constitution.md)).
- VII. Tested, traceable behavior: ✅ every AC names an `ExecutionManagerTests` test, except AC-005.2 (documentation only).

## Project Structure

### Documentation (this feature)

```text
specs/007-execution-manager/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `IExecutionManager.Cancel(string)` and `Cancel(Type)` return `bool`. `JobState.Scheduled` appended. `JobInfo.HandlerType` plus a new constructor overload. `JobStateSummary.Scheduled` plus a new constructor overload | **Yes** for implementers of `IExecutionManager`. `switch` statements over `JobState` gain a case |
| `ExecutionFlow.Hangfire` | `HangfireExecutionManager`: cancel Scheduled, lazy `GetJobs`, ExecutionFlow-only listing, UTC timestamps | Behavior |

```text
Src/ExecutionFlow/            IExecutionManager, JobInfo, JobState, JobStateSummary
Src/ExecutionFlow.Hangfire/   HangfireExecutionManager
Tests/                        ExecutionManagerTests
```

## Design

- **Cancel(string):**
  1. If `id` is a Hangfire job ID whose current state is active (Enqueued, Scheduled, Awaiting, Processing, from `GetStateData`), delete it directly.
  2. Otherwise, if the reservation key points to an active job, delete it.
  3. Otherwise scan Processing, each queue, and Scheduled for the custom ID.

  Return `IBackgroundJobClient.Delete`'s result, or `false` when nothing matched.
- **Cancel(Type):** also scan Scheduled (recurring jobs waiting for a retry). Return `bool`.
- **GetJobs:** an iterator with the connection opened inside it. Rows are read through `InfraUtils.ReadAll`, which already pages by 10. A row is kept when the job is an ExecutionFlow job: a loaded `Job` whose method is `HangfireJobDispatcher.DispatchEventAsync`/`DispatchRecurringAsync`, or, when the job can't be loaded, whose `InvocationData.Type` is `HangfireJobDispatcher`.
- **JobInfo:** for recurring jobs, `HandlerType` comes from the job arguments, and `EventType`/`EventTypeName` are null. For unloadable jobs, all three are null.
- **Scheduled:** `ScheduledJobs`, with `StateChangedAt = ScheduledAt`. `CountJobs(Scheduled)` and `JobStateSummary.Scheduled` read `stats.Scheduled`.
- **Timestamps:** `Kind = Unspecified` is treated as UTC before building the `DateTimeOffset`.

### Versioning impact

1.2.0 (the break for `IExecutionManager` implementers is accepted like the other 1.2.0 breaks).

### Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Callers relying on `GetJobs` being a snapshot | Different results when enumerated twice | XML docs and README |
| A foreach doing slow work per item | Holds a storage connection | Documented: `.ToList()` first when processing is slow |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --------- | ---------- | ------------------------------------ |
| Breaking `IExecutionManager` (principle V) | The caller must know whether a cancel worked (F-006) | Keeping `void` hides cancels that did nothing |
