# Plan: Deduplication redesign (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–5)
Status: approved

## Summary of the change
Replaces the Monitoring API scan with a reservation key per custom ID (F-001, F-003, F-005). Adds deduplication per event type through an attribute, and makes `FlowContext.SetCustomId` obsolete.

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `DeduplicationBehavior` moves here (namespace `ExecutionFlow.Abstractions`). New `ExecutionFlow.Attributes.DeduplicationAttribute`. `FlowContext<T>.SetCustomId` becomes `[Obsolete]` | **Yes (source)**: code that uses `DeduplicationBehavior` with only `using ExecutionFlow.Hangfire;` must add `using ExecutionFlow.Abstractions;` |
| `ExecutionFlow.Hangfire` | `ExecutionFlow.Hangfire.DeduplicationBehavior` removed. New options `DeduplicationLockTimeout` and `CreateOnDeduplicationLockTimeout`. New internal `DeduplicationStore` and `DeduplicationCleanupFilter` | See above |

## Design
- **Key:** a Hangfire hash `executionflow:dedup:{customId}` with fields `JobId` and `CreatedAt`, written with `SetRangeInHash`. The expiry is set through `JobStorageTransaction.ExpireHash` when the storage supports it: 30 days, plus the delay for scheduled jobs.
- **Publish** (`HangfireDispatcher.Dispatch`), only when the effective behavior isn't `Disabled` and the event has a non-empty custom ID:
  1. `AcquireDistributedLock("executionflow:dedup-lock:{customId}", DeduplicationLockTimeout)`. On `DistributedLockTimeoutException`: rethrow, or, when `CreateOnDeduplicationLockTimeout` is true, log `Trace.TraceWarning` and create without a reservation.
  2. Read the key. It is **active** when the referenced job's current state is Enqueued, Scheduled, Processing or Awaiting (`IStorageConnection.GetStateData`). Anything else is a stale key.
  3. `SkipIfExists` and active → `Enqueued = false`. `ReplaceExisting` and active → `Delete(jobId)`.
  4. Create the job (atomic custom ID, dispatching REQ-007), then write the key.
- **Effective behavior:** `[Deduplication]` on the event's **runtime class** wins, and otherwise `HangfireOptions.DeduplicationBehavior` applies.
- **Cleanup:** `DeduplicationCleanupFilter : IApplyStateFilter`, registered by `Build()`. When a job enters Succeeded, Deleted or Failed, it removes the key in the same transaction, if the key still points to that job.
- **Lookups:** `HangfireExecutionManager.IsRunning/IsPending/Cancel(string)` try the key first, as a fast path. When the key has no answer, they fall back to the existing scan, which covers jobs created without deduplication or before 1.2.0.
- **Scope:** only the custom ID given at publish time is reserved. `SetCustomId` doesn't move the key.

## Decisions
### DEC-001: Reservation key instead of a Monitoring API scan
- Context: F-003, F-005. The Monitoring API is for the dashboard, is O(n), and isn't atomic.
- Decision: a hash key per custom ID under a short lock per ID, following the `sidekiq-unique-jobs` model.
- Consequences: publishing with deduplication costs O(1) storage operations plus the lock. Jobs created before 1.2.0 have no key, so **deduplication doesn't see them** (lookups through the execution manager still do, through the scan). A crash between creating the job and writing the key can allow one duplicate.

### DEC-002: Lock timeout
- Decision: `DeduplicationLockTimeout` defaults to 1s. On timeout, `DistributedLockTimeoutException` is thrown. `CreateOnDeduplicationLockTimeout = true` creates the job without a reservation and logs a warning.

### DEC-003: Move the enum to `ExecutionFlow.Abstractions` in 1.2.0
- Context: the attribute lives on event classes, which reference only the core (P-003).
- Decision: move it and change the namespace, accepting a source break in a minor version. This is the maintainer's decision, which knowingly deviates from semver.
- Consequences: listed as a breaking change in the release notes.

### DEC-004: The attribute reads the runtime class
- Decision: `[Deduplication]` is read from `@event.GetType()`, because the attribute is declared on the class the user wrote. Routing still uses `TEvent` (dispatching RN-001).

## Versioning impact
1.2.0 (maintainer's decision, see DEC-003).

## Risks
| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Storage without `JobStorageTransaction` (no `ExpireHash`) | Keys aren't expired | Stale keys are detected on read. Documented |
| Jobs from before 1.2.0 aren't deduplicated against | A duplicate right after the upgrade | Release notes |
| Consumer on a version without the cleanup filter | Keys stay until they expire | Stale keys are detected on read |
