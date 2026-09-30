# Plan: Lifecycle hooks (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–7)
Status: approved

## Summary of the change
Fixes `Duration` (F-001), fires `OnFailed` only on the final failure (F-002), isolates hook exceptions (F-003), and moves `Duration` to the events where it means something (F-006). It also documents where hooks run and what scheduled jobs fire.

## Spike result (2026-09-30)
Running Hangfire 1.8.23's `AutomaticRetryAttribute.OnStateElection` on a real `ElectStateContext` with a candidate `FailedState(new InvalidOperationException("boom"))`:
- `Attempts = 3` → candidate becomes `Scheduled` ("Retry attempt 1 of 3: boom"), and `TraversedStates` holds the original `FailedState` **with its exception**.
- `Attempts = 0` → the candidate stays `Failed`.

So a filter that runs **after** the retry filter sees the final decision and still has the exception.

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `ExecutionEvent.Duration` and its ctor parameter removed. `Duration` added to `ExecutionSucceededEvent`, `ExecutionFailedEvent`, `ExecutionRetryingEvent`. `ExecutionRetryingEvent.Exception` added. New `HookErrorContext` | **Yes (source)**: `Duration` on a base `ExecutionEvent`, and the `ExecutionEvent` ctor's `duration` argument |
| `ExecutionFlow.Hangfire` | `HangfireStateFilter` registered with order 100. New `HangfireOptions.HookErrorHandler` | Behavior: `OnFailed` only on the final failure |

## Design
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

## Decisions
### DEC-001: Run the state filter after the retry filter
- Context: F-002. The spike shows that `TraversedStates` keeps the failed state and its exception.
- Decision: order 100, and derive retry versus final failure from the candidate plus `TraversedStates`.
- Consequences: a custom filter with order > 100 that changes the candidate state after us would again cause hooks to fire on a state that isn't final. Documented.

### DEC-002: A deleted-after-exhausted-retries job is a failure
- Decision: `OnFailed`, because from the hook author's point of view the job failed for good.

### DEC-003: No hook when a retry becomes due
- Decision: `OnRetrying` fires once per retry, when the retry is scheduled.

## Versioning impact
1.2.0 (source break on `Duration`, accepted as with the other 1.2.0 breaks).

## Risks
| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Users relying on `OnFailed` for every attempt | They miss intermediate failures | `OnRetrying` now carries the exception. Release notes |
| Hooks that threw on purpose to affect the job | They no longer affect it | Documented: use a Hangfire filter |
