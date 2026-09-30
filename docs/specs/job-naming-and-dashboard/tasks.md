# Tasks: Job naming and dashboard (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Move `ICustomNameEvent` to the core
  - Requirements: REQ-002
  - Depends on: —
  - Done when: the solution (including the examples) builds

- [x] TASK-002: `HangfireJobInfo.Create` recognizes only ExecutionFlow jobs; non-ExecutionFlow jobs get Hangfire's default name; `IJobIdGenerator` is out of the naming chain
  - Requirements: REQ-001, REQ-003
  - Depends on: —
  - Done when: the tests for native jobs (plain, generic, `[JobDisplayName]`) pass

- [x] TASK-003: `[JobDisplayName]` on handlers as a naming source, with `{0}` = the event
  - Requirements: REQ-001
  - Depends on: TASK-002
  - Done when: the precedence and formatting tests pass

- [x] TASK-004: The dashboard falls back to default names when `IHangfireJobName` is missing
  - Requirements: REQ-003
  - Depends on: TASK-002
  - Done when: the dashboard test with no registration passes

- [x] TASK-005: README, spec, findings
  - Depends on: TASK-001..TASK-004
  - Done when: the spec is `approved` and every AC names an existing test
