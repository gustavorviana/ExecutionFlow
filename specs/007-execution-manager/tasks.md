---
description: "Tasks for the execution manager (migrated; all complete)"
---

# Tasks: Execution manager

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Foundational - Contract changes

- [x] T001 [US2] Contract changes in `Src/ExecutionFlow/`: `Cancel` returns `bool`, `JobState.Scheduled`, `JobInfo.HandlerType`, `JobStateSummary.Scheduled` (FR-002, FR-004, FR-005; depends on: none; done when the solution builds)

## Phase 2: User Story 2 - Cancel (Priority: P1)

- [x] T002 [US2] `Cancel` finds Scheduled jobs; an exact Hangfire ID is deleted directly (FR-002; depends on T001; done when the cancel tests for Scheduled, exact ID and return value pass)

## Phase 3: User Story 4 - List and count (Priority: P2)

- [x] T003 [P] [US4] Lazy `GetJobs`, ExecutionFlow-only, Scheduled, `HandlerType` for recurring jobs, UTC timestamps (FR-004; depends on T001; done when the lazy, filter, recurring and timestamp tests pass)
- [x] T004 [P] [US4] `CountJobs`/`GetStateSummary` support Scheduled (FR-005; depends on T001; done when the count tests for Scheduled pass)

## Phase 4: Polish

- [x] T005 README, XML docs, spec, findings (all requirements; depends on T001..T004; done when the README documents lazy `GetJobs`, cancelling Scheduled jobs, `Cancel`'s result and storage-wide counts; the spec is `approved`; every AC names an existing test)
