---
description: "Tasks for interval schedules, overlap control and the plan in the core"
---

# Tasks: Interval schedules, overlap control and the plan in the core

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Write each task's tests first; the task is done when the tests of the acceptance scenarios it covers pass, together with the existing suite.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: User Story 3 - The plan in the core (Priority: P2, done first because 011 depends on it)

- [ ] T001 [US3] `ExecutionFlowOptions.UsePlan` + `OnPlanRegistered` hook in `Src/ExecutionFlow/ExecutionFlowOptions.cs`; move `HangfireOptions.UsePlan` body into the override in `Src/ExecutionFlow.Hangfire/HangfireOptions.cs` (FR-004, SC-001; tests `ExecutionFlowOptionsTests.UsePlan_*`, `ExecutionPlanHangfireTests.UsePlan_ThroughBaseOptions_RegistersPlan`, and every existing `ExecutionPlanHangfireTests` unchanged)

## Phase 2: User Story 1 - Interval schedules (Priority: P1)

- [ ] T002 [P] [US1] `RecurringIntervalAttribute`, `RecurringJobRegistryInfo.Interval`, and `Configure` validation in `Src/ExecutionFlow/` (FR-001; tests `IntervalScheduleTests`)
- [ ] T003 [US1] `IntervalCron.TryGetExactCron` and interval registration in `HangfireSetup` (FR-002, AC-002.1..AC-002.3; depends on T002; tests `IntervalRegistrationTests`)
- [ ] T004 [US1] `IntervalTickerStore` and `IntervalTickerFilter` (FR-002, AC-002.4, AC-002.5, SC-002; depends on T003; tests `IntervalTickerTests`)

## Phase 3: User Story 2 - Overlap control (Priority: P1)

- [ ] T005 [P] [US2] `NoOverlapAttribute`, `ExecutionPlanEntry.NoOverlap`, `ExecutionPlanStep.OverlapTimeout`, registry value and validation (FR-003, AC-003.1, AC-003.2; tests `OverlapOptionTests`, `ExecutionPlannerTests.Build_PrefersEntryOverlap_OverAttribute`)
- [ ] T006 [US2] `NoOverlapFilter` in Hangfire (FR-003, AC-003.3..AC-003.5; depends on T005; tests `OverlapFilterTests`)

## Phase 4: Polish

- [ ] T007 READMEs (core and Hangfire) for `[RecurringInterval]`, `[NoOverlap]`, `UsePlan` on the base options, and the clock-aligned vs host-start difference; the README and the XML docs of `RecurringIntervalAttribute` say cron is preferred in Hangfire and the interval is an alternative (documentation requirement) (depends on T001..T006; done when the README examples compile in `Examples/`)
