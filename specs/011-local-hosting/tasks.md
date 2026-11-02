---
description: "Tasks for local hosting (migrated; none checked in the source)"
---

# Tasks: Local hosting

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers. Check state copied as-is from `docs/specs/local-hosting/tasks.md`.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [ ] T001 [US1] Create the package project, add the `IClock` seam with the netstandard2.0 polyfill, and add the project to the solution (FR-009, SC-002; depends on [010-execution-plan](../010-execution-plan/tasks.md) T003, [005-recurring-jobs](../005-recurring-jobs/tasks.md) T009; done when it builds for both TFMs)

## Phase 2: Foundational

- [ ] T002 [P] [US2] `DependencyGate` (FR-005; depends on T001; done when `DependencyGateTests` pass)
- [ ] T003 [P] [US4] `ConcurrencyLimiter`, `HostingOptions` and `HandlerExecutor` (FR-002, FR-003, FR-006; depends on T001; done when `HostingOptionsTests` and `ConcurrencyLimiterTests` pass)

## Phase 3: User Stories 1, 2, 5 - Scheduled execution (Priority: P1)

- [ ] T004 [US1] `RecurringHandlerHost<T>` (FR-003, FR-004, FR-005; depends on T002, T003; done when `RecurringHandlerHostTests` pass)

## Phase 4: User Story 3 - Run once (Priority: P2)

- [ ] T005 [US3] `ExecutionPlanRunner` and `RunOnceHostedService` (FR-007; depends on T003; done when `ExecutionPlanRunnerTests` and `RunOnceHostedServiceTests` pass)

## Phase 5: Registration and startup report (Priority: P1/P3)

- [ ] T006 [US6] `AddExecutionFlowHosting` and `StartupReportHostedService` (FR-001, FR-002, FR-008; depends on T004, T005; done when `ServiceCollectionExtensionsTests` and `StartupReportHostedServiceTests` pass)

## Phase 6: Polish

- [ ] T007 Example, READMEs and publish workflow (requirements: none; depends on T006; done when the example runs in scheduled and run-once modes)
