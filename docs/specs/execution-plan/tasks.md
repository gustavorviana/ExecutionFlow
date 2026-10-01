# Tasks: Execution plan (v1.3.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: `DependsOnAttribute`, `ExecutionPlanner` and `ExecutionPlanEntry`
  - Requirements: REQ-001, REQ-002, REQ-003
  - Depends on: —
  - Done when: the declaration tests pass

- [x] TASK-002: `Build` (cycles, order, availability policy), `ExecutionPlan` and `ExecutionPlanStep`
  - Requirements: REQ-003, REQ-004, REQ-005, REQ-006
  - Depends on: TASK-001
  - Done when: all `ExecutionPlannerTests` pass

- [x] TASK-003: Run modes on entries and steps (`MinInterval`, `RunOnOwnSchedule`) and their validation
  - Requirements: REQ-007
  - Depends on: TASK-002
  - Done when: the REQ-007 tests in `ExecutionPlannerTests` pass
