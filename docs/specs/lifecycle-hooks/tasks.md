# Tasks: Lifecycle hooks (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Move `Duration` to Succeeded/Failed/Retrying, and add `ExecutionRetryingEvent.Exception`
  - Requirements: REQ-003
  - Depends on: —
  - Done when: the solution builds

- [x] TASK-002: Parse `StartedAt` as UTC
  - Requirements: AC-003.2
  - Depends on: —
  - Done when: a duration test that stores a UTC `StartedAt` gets a value close to the real one on any host

- [x] TASK-003: Filter order 100, and the new state mapping (final failure, retry once, manual requeue, delete after exhausted retries)
  - Requirements: REQ-002
  - Depends on: TASK-001
  - Done when: state filter tests that run the real `AutomaticRetryAttribute` before our filter pass

- [x] TASK-004: Isolate hook exceptions, and `HangfireOptions.HookErrorHandler`
  - Requirements: REQ-004
  - Depends on: —
  - Done when: a throwing hook doesn't stop the other hooks or the election, and the handler receives the error

- [x] TASK-005: README, spec, findings
  - Requirements: all
  - Depends on: TASK-001..TASK-004
  - Done when: the README documents where hooks run, `Duration` per event, `OnFailed`/`OnRetrying` semantics, isolation and `HookErrorHandler`, scheduled jobs, and keeping hooks fast; the spec is `approved`; every AC names an existing test
