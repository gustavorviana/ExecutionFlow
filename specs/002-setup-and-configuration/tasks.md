---
description: "Tasks for setup and configuration (migrated; all complete)"
---

# Tasks: Setup and configuration

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers. "to-be item n" refers to the old spec's decided to-be changes.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Stories 2, 4 - Full build isolation (Priority: P1)

- [x] T001 [US2] Own activator per setup, and the last full build replaces the previous global filters in `Src/ExecutionFlow.Hangfire/HangfireSetup.cs` (FR-002, to-be item 1; done when tests show no filter accumulation across two builds, and that a build never uses another setup's activator)

## Phase 2: User Story 1 - Locked options (Priority: P1)

- [x] T002 [P] [US1] Option property setters throw after `Configure` (AC-001.3, to-be item 2; done when a setter test per options class passes)

## Phase 3: User Stories 2, 3 - DI and producer-only (Priority: P1/P2)

- [x] T003 [US2] `StartExecutionFlow()` and a renamed startup service (FR-005, to-be item 3; done when a DI test without a host shows `StartExecutionFlow()` runs the build)
- [x] T004 [US3] Forbid full + producer-only in one container (FR-005, to-be item 4; done when tests for both registration orders throw with the guidance message)
- [x] T005 [US3] `IExecutionManager` in producer-only (FR-003, to-be item 5; done when DI and non-DI producer-only tests resolve a manager bound to the dispatcher's storage)

## Phase 4: User Story 4 - Reliable built-in activator (Priority: P2)

- [X] T008 [US4] In `Src/ExecutionFlow.Hangfire/Infrastructure/FlowEngineJobActivator.cs`: detect constructor cycles (AC-004.2), create registered singletons once under concurrency (AC-004.3), and cache each type's constructor and parameters, keeping what it creates and every FR-004 lifetime unchanged (done when AC-004.1..AC-004.3 tests pass and no existing test changed)
- [X] T009 [US4] XML docs of `HangfireSetup.ConfigureActivator`, `Build` and `BuildDispatcherOnly`, and the README's "Without DI" sections: the built-in activator is an internal fallback, not the app's DI; recommend `AddHangfireToExecutionFlow` (Clarifications 2026-11-03; done when the docs build without warnings)

## Phase 5: Polish

- [x] T006 Lower the dependency floor and pin `Hangfire.Console` (to-be item 6, RN-005; done when the solution builds and the constitution's open question is closed)
- [x] T007 README, spec, findings (done when the spec is `approved` and every AC names an existing test)
