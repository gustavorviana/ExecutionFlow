---
description: "Tasks for logging and console (migrated; all complete)"
---

# Tasks: Logging and console

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers. "Item n" refers to the spec's "Decided to-be changes".

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Stories 3, 1 - Isolation and logger context (Priority: P1)

- [x] T001 [US3] Isolate loggers in `CompositeExecutionLogger`, and dedupe `AddLogger` (AC-002.2, AC-003.2; done when throwing-logger and duplicate-registration tests pass)
- [x] T002 [US1] `ExecutionLoggerContext`, `IExecutionLoggerContextFactory`, `FlowContextBuilder.SetHandler`; the dispatcher sets it (item 2; done when a context factory receives the job and handler facts)

## Phase 2: User Story 2 - Hangfire console (Priority: P2)

- [x] T003 [US2] Console: `ILogger`-style templates, `MinimumLevel`, `CreateProgressBar` via `GetPerformContext()`, and tests (F-007) (FR-003, FR-004; done when formatting, level and console-logger tests pass)

## Phase 3: User Story 4 - Microsoft.Extensions.Logging bridge (Priority: P2)

- [x] T004 [US4] New package `ExecutionFlow.Extensions.Logging` with its test project, in the solution (item 2; done when the bridge tests pass and `dotnet pack` includes it)

## Phase 4: Polish

- [x] T005 READMEs (root, Console, new package), spec, findings (done when the spec is `approved` and every AC names an existing test)
