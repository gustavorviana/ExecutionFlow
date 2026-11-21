---
description: "Tasks for handlers and context (migrated; all complete)"
---

# Tasks: Handlers and context

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Story 3 - Protected parameters (Priority: P2)

- [x] T001 [P] [US3] Case-insensitive read-only keys in `Src/ExecutionFlow/Abstractions/FlowParameters.cs` (AC-003.2; done when tests for case-variant overwrite and removal throw)

## Phase 2: User Story 2 - Job facts (Priority: P2)

- [x] T002 [US2] `FlowContext.JobId`/`AttemptNumber` and `FlowContextBuilder.SetJob`, set by `HangfireJobDispatcher` (FR-002; done when builder tests and dispatcher tests (first attempt, retry) pass)
- [x] T003 [US3] Public keys and `GetPerformContext()` (AC-003.3; done when extension tests pass and `ContextConsts` uses the public keys). Note: the keys finally stayed internal (F-003).

## Phase 3: Polish

- [x] T004 README, spec, findings (done when the spec is `approved` and every AC names an existing test)
