---
description: "Tasks for recurring jobs (migrated; all complete)"
---

# Tasks: Recurring jobs

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Foundational (v1.2.0)

- [x] T001 [P] [US5] `RecurringAttribute.Id`/`TimeZone`, and `RecurringJobRegistryInfo.Id`/`TimeZone` (FR-007, FR-008; depends on —; done when the attribute values reach the registry (`ExecutionFlowOptionsTests`))
- [x] T003 [P] [US6] `HangfireOptions.RecurringTimeZone` and `SetJobTimeZone` (FR-008; depends on —; done when the precedence tests pass)

## Phase 2: Validation and registration (v1.2.0)

- [x] T002 [US1] Validation in `OnConfigured` (missing cron, unknown time zone, duplicate IDs) (AC-001.3, FR-007, FR-008; depends on T001, T003; done when the `RecurringValidationTests` pass, and existing tests have `[Recurring]` where they register recurring handlers)
- [x] T004 [US1] `RegisterRecurring` resolves the ID, time zone and on/off (`Cron.Never`). `Trigger(Type)` uses the same ID resolver (FR-002, FR-003, FR-004, FR-007, FR-008; depends on T001, T003; done when `RecurringRegistrationTests` pass)
- [x] T005 [US4] Orphan cleanup only removes ExecutionFlow recurring jobs (FR-006; depends on T004; done when the orphan tests pass)
- [x] T006 [US2] Stop registering `HangfireAutoRunFilter`, and mark it `[Obsolete]` (FR-003, FR-004; depends on T004; done when `Build()` doesn't register it (`HangfireSetupGlobalFiltersTests`))

## Phase 3: Polish (v1.2.0)

- [x] T007 README, spec, findings (all requirements; depends on T001..T006; done when the README documents `Id`, `TimeZone`, on/off per storage, queues and `[DisableConcurrentExecution]`, the spec is `approved`, and every AC names an existing test)

## Phase 4: User Story 7 - Execution plan in Hangfire (v1.3.0)

- [x] T008 [US7] `HangfireOptions.UsePlan` (registers the handlers and their auto-run) and job naming from the plan (FR-009; depends on [010-execution-plan](../010-execution-plan/tasks.md) T002; done when `ExecutionPlanHangfireTests` pass)
- [x] T009 [US7] `PrerequisitesNotMetState`, `PrerequisiteGenerationStore`, `PrerequisiteGateFilter` and the increment in the dispatcher (FR-010; depends on T008; done when `PrerequisiteGateTests` pass)
- [x] T010 [P] [US7] `JobState.PrerequisitesNotMet` (FR-011; depends on —; done when the `ExecutionManagerTests` case passes)
- [x] T011 [US7] Run modes (`RunOnOwnSchedule`, `MinInterval`), no-overlap retrigger, and manual runs without the gate (FR-012; depends on T009, [010-execution-plan](../010-execution-plan/tasks.md) T003; done when the FR-012 tests in `PrerequisiteGateTests`, `ExecutionPlanHangfireTests` and `RecurringRegistrationTests` pass)
