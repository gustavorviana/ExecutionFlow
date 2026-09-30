# Plan: Execution manager (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–7)
Status: approved

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `IExecutionManager.Cancel(string)` and `Cancel(Type)` return `bool`. `JobState.Scheduled` appended. `JobInfo.HandlerType` plus a new constructor overload. `JobStateSummary.Scheduled` plus a new constructor overload | **Yes** for implementers of `IExecutionManager`. `switch` statements over `JobState` gain a case |
| `ExecutionFlow.Hangfire` | `HangfireExecutionManager`: cancel Scheduled, lazy `GetJobs`, ExecutionFlow-only listing, UTC timestamps | Behavior |

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

## Decisions
### DEC-001: Lazy `GetJobs` instead of a paged overload
- Maintainer's proposal: same signature and bounded memory, with paging through LINQ `Skip`/`Take`.
- Consequences: deferred execution, re-enumeration hits the storage again, the connection stays open during enumeration, and there's no snapshot. All documented.

### DEC-002: An exact Hangfire ID is deleted without a scan
- It's O(1), and it's the only way to cancel a Scheduled job that has no custom ID.

## Versioning impact
1.2.0 (the break for `IExecutionManager` implementers is accepted like the other 1.2.0 breaks).

## Risks
| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Callers relying on `GetJobs` being a snapshot | Different results when enumerated twice | XML docs and README |
| A foreach doing slow work per item | Holds a storage connection | Documented: `.ToList()` first when processing is slow |
