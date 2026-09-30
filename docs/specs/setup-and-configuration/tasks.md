# Tasks: Setup and configuration (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Own activator per setup, and the last full build replaces the previous global filters
  - Requirements: REQ-002 (to-be item 1)
  - Done when: tests show no filter accumulation across two builds, and that a build never uses another setup's activator

- [x] TASK-002: Option property setters throw after `Configure`
  - Requirements: AC-001.3 (to-be item 2)
  - Done when: a setter test per options class passes

- [x] TASK-003: `StartExecutionFlow()` and a renamed startup service
  - Requirements: REQ-005 (to-be item 3)
  - Done when: a DI test without a host shows `StartExecutionFlow()` runs the build

- [x] TASK-004: Forbid full + producer-only in one container
  - Requirements: REQ-005 (to-be item 4)
  - Done when: tests for both registration orders throw with the guidance message

- [x] TASK-005: `IExecutionManager` in producer-only
  - Requirements: REQ-003 (to-be item 5)
  - Done when: DI and non-DI producer-only tests resolve a manager bound to the dispatcher's storage

- [x] TASK-006: Lower the dependency floor and pin `Hangfire.Console`
  - Requirements: to-be item 6
  - Done when: the solution builds and the constitution's open question is closed

- [x] TASK-007: README, spec, findings
  - Done when: the spec is `approved` and every AC names an existing test
