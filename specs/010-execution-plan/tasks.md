---
description: "Tasks for the execution plan (migrated; all complete)"
---

# Tasks: Execution plan

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Story 1 - Declare dependencies (Priority: P1)

- [x] T001 [US1] `DependsOnAttribute`, `ExecutionPlanner` and `ExecutionPlanEntry` in `Src/ExecutionFlow/Abstractions/` and `Src/ExecutionFlow/Attributes/` (FR-001, FR-002, FR-003; done when the declaration tests in `ExecutionPlannerTests` pass)

## Phase 2: User Stories 2, 3, 4 - Validate, disable and order (Priority: P1/P2)

- [x] T002 [US2] `Build` (cycles, order, availability policy), `ExecutionPlan` and `ExecutionPlanStep` in `Src/ExecutionFlow/Abstractions/` (FR-003..FR-006; depends on T001; done when all `ExecutionPlannerTests` pass)

## Phase 3: User Story 1 - Run modes (Priority: P1)

- [x] T003 [US1] Run modes on entries and steps (`MinInterval`, `RunOnOwnSchedule`) and their validation (FR-007; depends on T002; done when the FR-007 tests in `ExecutionPlannerTests` pass)
