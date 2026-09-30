# Tasks: Execution manager (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Contract changes: `Cancel` returns `bool`, `JobState.Scheduled`, `JobInfo.HandlerType`, `JobStateSummary.Scheduled`
  - Requirements: REQ-002, REQ-004, REQ-005
  - Depends on: —
  - Done when: the solution builds

- [x] TASK-002: `Cancel` finds Scheduled jobs; an exact Hangfire ID is deleted directly
  - Requirements: REQ-002
  - Depends on: TASK-001
  - Done when: the cancel tests for Scheduled, exact ID and return value pass

- [x] TASK-003: Lazy `GetJobs`, ExecutionFlow-only, Scheduled, `HandlerType` for recurring jobs, UTC timestamps
  - Requirements: REQ-004
  - Depends on: TASK-001
  - Done when: the lazy, filter, recurring and timestamp tests pass

- [x] TASK-004: `CountJobs`/`GetStateSummary` support Scheduled
  - Requirements: REQ-005
  - Depends on: TASK-001
  - Done when: the count tests for Scheduled pass

- [x] TASK-005: README, XML docs, spec, findings
  - Requirements: all
  - Depends on: TASK-001..TASK-004
  - Done when: the README documents lazy `GetJobs`, cancelling Scheduled jobs, `Cancel`'s result and storage-wide counts; the spec is `approved`; every AC names an existing test
