---
description: "Tasks for local hosting (regenerated 2026-11-02 after /speckit-analyze)"
---

# Tasks: Local hosting

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Write each task's tests first; the task is done when the tests of the acceptance scenarios it covers pass.

**Numbering**: T001..T007 keep their IDs from the migrated list with revised content; T008+ are new (constitution VII: IDs are never renumbered).

**Depends on**: [012-interval-and-overlap](../012-interval-and-overlap/tasks.md) T001, T002, T005.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [ ] T001 [US1] Delete the stale `Src/ExecutionFlow.Hosting/bin` and `obj`; create `Src/ExecutionFlow.Hosting/ExecutionFlow.Hosting.csproj` (`netstandard2.0;net8.0`), the `IClock` seam with `SystemClock` and `TimeProviderClock`, and add it to `ExecutionFlow.slnx` (FR-009, SC-002; depends on 012 T001; done when it builds for both TFMs)
- [ ] T008 [US1] Create `Tests/ExecutionFlow.Hosting.Tests`, add `Microsoft.Extensions.TimeProvider.Testing` to `Directory.Packages.props`, and a test run against the `netstandard2.0` asset for `SystemClockTests` (FR-009, AC-009.2, SC-002; depends on T001; done when `SystemClockTests.DelayAndWait_CompleteAndHonorCancellation` passes on both assets)

## Phase 2: Foundational

- [ ] T002 [P] [US2] `DependencyGate` (generations, consumed generations, atomic trigger selection) (FR-005, AC-005.1..AC-005.3, AC-005.8; depends on T001; done when `DependencyGateTests` pass)
- [ ] T003 [P] [US4] `ConcurrencyLimiter`, `HostingOptions` (enabled, global/per-handler settings, `OnPlanRegistered`) and `HandlerExecutor` (FR-002, FR-003, FR-006, AC-002.2, AC-002.4, AC-003.11, AC-006.1, AC-006.2; depends on T001, 012 T001; done when `HostingOptionsTests` and `ConcurrencyLimiterTests` pass)
- [ ] T009 [P] [US1] `OverlapGuard` (wait with mandatory timeout, skip mode) (FR-003, AC-003.8..AC-003.10; depends on T001, 012 T005; done when the overlap cases of `RecurringHandlerHostTests` pass)

## Phase 3: User Stories 1, 5 - Scheduled execution (Priority: P1)

- [ ] T004 [US1] `RecurringHandlerHost` for cron and interval schedules (host-start cadence, `IntervalFromEnd`), failure isolation and quiet shutdown (FR-003, FR-004, AC-003.1..AC-003.7, AC-004.1..AC-004.3, AC-009.1; depends on T002, T003, T009, 012 T002; done when those `RecurringHandlerHostTests` pass)

## Phase 4: User Story 2 - Execution plan (Priority: P1)

- [ ] T010 [US2] Dependent triggers: default mode, `MinInterval` postponement, `RunOnOwnSchedule` skip, no-overlap re-check, cancellation on stop (FR-005, AC-005.4..AC-005.7, AC-005.9; depends on T004; done when those `RecurringHandlerHostTests` pass)
- [ ] T011 [US2] SC-001 measurement test `RecurringHandlerHostTests.WaitingRuns_HoldNoThreads` (SC-001; depends on T010)

## Phase 5: User Story 3 - Run once (Priority: P2)

- [ ] T005 [US3] `ExecutionPlanRunner` and `RunOnceHostedService` (FR-007, AC-007.1..AC-007.8; depends on T003; done when `ExecutionPlanRunnerTests` and `RunOnceHostedServiceTests` pass)

## Phase 6: Registration and startup report (Priority: P1/P3)

- [ ] T006 [US1] `AddExecutionFlowHosting` and `StartupReportHostedService` (FR-001, FR-002, FR-008, AC-001.1, AC-001.3..AC-001.5, AC-002.1, AC-002.3, AC-002.5, AC-008.1; depends on T004, T005, T010; done when `ServiceCollectionExtensionsTests` and `StartupReportHostedServiceTests` pass)

- [ ] T012 [US1] Host isolation tests (FR-010, AC-010.1, AC-010.2; depends on T010; done when `HostIsolationTests` pass)

## Phase 7: Polish

- [ ] T007 Example, READMEs and add the package to the publish workflow; README and XML docs of `AddExecutionFlowHosting`/`HostingOptions` state that the host is local and isolated (FR-010 documentation requirement) (depends on T006, T012; done when the example runs in scheduled and run-once modes)
