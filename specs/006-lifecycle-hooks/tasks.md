---
description: "Tasks for lifecycle hooks (migrated; all complete)"
---

# Tasks: Lifecycle hooks

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Foundational

- [x] T001 [US2] Move `Duration` to Succeeded/Failed/Retrying, and add `ExecutionRetryingEvent.Exception` (FR-003; depends on —; done when the solution builds)
- [x] T002 [P] [US2] Parse `StartedAt` as UTC (AC-003.2; depends on —; done when a duration test that stores a UTC `StartedAt` gets a value close to the real one on any host)

## Phase 2: User Stories 1 and 3 - Final failures and retries (Priority: P1)

- [x] T003 [US1] Filter order 100, and the new state mapping (final failure, retry once, manual requeue, delete after exhausted retries) (FR-002; depends on T001; done when state filter tests that run the real `AutomaticRetryAttribute` before our filter pass)

## Phase 3: User Story 4 - Hook isolation (Priority: P1)

- [x] T004 [P] [US4] Isolate hook exceptions, and `HangfireOptions.HookErrorHandler` (FR-004; depends on —; done when a throwing hook doesn't stop the other hooks or the election, and the handler receives the error)

## Phase 4: Polish

- [x] T005 README, spec, findings (all requirements; depends on T001..T004; done when the README documents where hooks run, `Duration` per event, `OnFailed`/`OnRetrying` semantics, isolation and `HookErrorHandler`, scheduled jobs, and keeping hooks fast; the spec is `approved`; every AC names an existing test)
