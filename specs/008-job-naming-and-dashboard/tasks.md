---
description: "Tasks for job naming and dashboard (migrated; all complete)"
---

# Tasks: Job naming and dashboard

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Story 2 - Per-instance name in the core (Priority: P2)

- [x] T001 [P] [US2] Move `ICustomNameEvent` to the core (FR-002; depends on: none; done when the solution, including the examples, builds)

## Phase 2: User Stories 3 and 1 - Recognize ExecutionFlow jobs (Priority: P1)

- [x] T002 [P] [US3] `HangfireJobInfo.Create` recognizes only ExecutionFlow jobs; non-ExecutionFlow jobs get Hangfire's default name; `IJobIdGenerator` is out of the naming chain (FR-001, FR-003; depends on: none; done when the tests for native jobs (plain, generic, `[JobDisplayName]`) pass)

## Phase 3: User Stories 4 and 1 - `[JobDisplayName]` and dashboard (Priority: P1/P3)

- [x] T003 [US4] `[JobDisplayName]` on handlers as a naming source, with `{0}` = the event (FR-001; depends on T002; done when the precedence and formatting tests pass)
- [x] T004 [US1] The dashboard falls back to default names when `IHangfireJobName` is missing (FR-003 as listed in the old tasks; behavior specified in FR-004; depends on T002; done when the dashboard test with no registration passes)

## Phase 4: Polish

- [x] T005 README, spec, findings (depends on T001..T004; done when the spec is `approved` and every AC names an existing test)
