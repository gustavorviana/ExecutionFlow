# Implementation Plan: Deduplication redesign (v1.2.0)

**Branch**: N/A (migrated) | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/custom-id-and-deduplication/plan.md` (status: approved). Decisions are in [research.md](research.md).

## Summary

Replaces the Monitoring API scan with a reservation key per custom ID (F-001, F-003, F-005). Adds deduplication per event type through an attribute, and makes `FlowContext.SetCustomId` obsolete.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: none in the core; Hangfire.Core 1.8 in `ExecutionFlow.Hangfire`

**Storage**: Hangfire job storage (hash per custom ID, distributed lock)

**Testing**: xUnit + NSubstitute (`DispatcherTests`, `DeduplicationStoreTests`, `DeduplicationCleanupFilterTests`, `DeduplicationAttributeTests`, `ExecutionManagerTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: O(1) storage operations plus one lock per deduplicated publish (RN-006)

**Constraints**: wait at most `DeduplicationLockTimeout` (SC-001)

**Scale/Scope**: one key per active custom ID

## Constitution Check

- I. Dependency-free core: ✅ `DeduplicationBehavior` and `DeduplicationAttribute` in the core have no dependencies.
- II. Processors isolated: ✅ storage, lock and cleanup live in `ExecutionFlow.Hangfire`. Moving the enum to the core is required because the attribute lives on event classes, which reference only the core.
- IV. Producer-only mode mutates no global state: ✅ `BuildDispatcherOnly()` doesn't register the cleanup filter (AC-008.0).
- V. Minimal public surface: ✅ `DeduplicationStore` and `DeduplicationCleanupFilter` are internal.
- VI. Dispatch doesn't block: ✅ a short lock per custom ID, only when deduplication applies.
- VII. Tested, traceable behavior: ✅ every AC names a test, except AC-005.2 (Hangfire behavior).
- Versioning and Release: ⚠️ source break in a minor version (see Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/004-custom-id-and-deduplication/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `DeduplicationBehavior` moves here (namespace `ExecutionFlow.Abstractions`). New `ExecutionFlow.Attributes.DeduplicationAttribute`. `FlowContext<T>.SetCustomId` becomes `[Obsolete]` | **Yes (source)**: code that uses `DeduplicationBehavior` with only `using ExecutionFlow.Hangfire;` must add `using ExecutionFlow.Abstractions;` |
| `ExecutionFlow.Hangfire` | `ExecutionFlow.Hangfire.DeduplicationBehavior` removed. New options `DeduplicationLockTimeout` and `CreateOnDeduplicationLockTimeout`. New internal `DeduplicationStore` and `DeduplicationCleanupFilter` | See above |

```text
Src/ExecutionFlow/
├── Abstractions/DeduplicationBehavior.cs
└── Attributes/DeduplicationAttribute.cs
Src/ExecutionFlow.Hangfire/Infrastructure/
├── HangfireDispatcher.cs
├── HangfireExecutionManager.cs
├── DeduplicationStore.cs
└── DeduplicationCleanupFilter.cs
```

## Design

- **Key:** a Hangfire hash `executionflow:dedup:{customId}` with fields `JobId` and `CreatedAt`, written with `SetRangeInHash`. The expiry is set through `JobStorageTransaction.ExpireHash` when the storage supports it: 30 days, plus the delay for scheduled jobs.
- **Publish** (`HangfireDispatcher.Dispatch`), only when the effective behavior isn't `Disabled` and the event has a non-empty custom ID:
  1. `AcquireDistributedLock("executionflow:dedup-lock:{customId}", DeduplicationLockTimeout)`. On `DistributedLockTimeoutException`: rethrow, or, when `CreateOnDeduplicationLockTimeout` is true, log `Trace.TraceWarning` and create without a reservation.
  2. Read the key. It is **active** when the referenced job's current state is Enqueued, Scheduled, Processing or Awaiting (`IStorageConnection.GetStateData`). Anything else is a stale key.
  3. `SkipIfExists` and active → `Enqueued = false`. `ReplaceExisting` and active → `Delete(jobId)`.
  4. Create the job (atomic custom ID, [003-dispatching](../003-dispatching/spec.md) FR-007), then write the key.
- **Effective behavior:** `[Deduplication]` on the event's **runtime class** wins, and otherwise `HangfireOptions.DeduplicationBehavior` applies.
- **Cleanup:** `DeduplicationCleanupFilter : IApplyStateFilter`, registered by `Build()`. When a job enters Succeeded, Deleted or Failed, it removes the key in the same transaction, if the key still points to that job.
- **Lookups:** `HangfireExecutionManager.IsRunning/IsPending/Cancel(string)` try the key first, as a fast path. When the key has no answer, they fall back to the existing scan, which covers jobs created without deduplication or before 1.2.0.
- **Scope:** only the custom ID given at publish time is reserved. `SetCustomId` doesn't move the key.

## Versioning impact

1.2.0 (maintainer's decision, see [research.md](research.md) R-003).

## Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Storage without `JobStorageTransaction` (no `ExpireHash`) | Keys aren't expired | Stale keys are detected on read. Documented |
| Jobs from before 1.2.0 aren't deduplicated against | A duplicate right after the upgrade | Release notes |
| Consumer on a version without the cleanup filter | Keys stay until they expire | Stale keys are detected on read |

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| --------- | ---------- | ------------------------------------ |
| Source break (`DeduplicationBehavior` namespace) in a minor version | The attribute lives on event classes, which reference only the core (principle II) | Waiting for 2.0 would delay per-event deduplication; maintainer's decision (R-003) |
