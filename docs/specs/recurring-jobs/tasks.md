# Tasks: Recurring jobs (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: `RecurringAttribute.Id`/`TimeZone`, and `RecurringJobRegistryInfo.Id`/`TimeZone`
  - Requirements: REQ-007, REQ-008
  - Depends on: —
  - Done when: the attribute values reach the registry (`ExecutionFlowOptionsTests`)

- [x] TASK-002: Validation in `OnConfigured` (missing cron, unknown time zone, duplicate IDs)
  - Requirements: AC-001.3, REQ-007, REQ-008
  - Depends on: TASK-001, TASK-003
  - Done when: the `RecurringValidationTests` pass, and existing tests have `[Recurring]` where they register recurring handlers

- [x] TASK-003: `HangfireOptions.RecurringTimeZone` and `SetJobTimeZone`
  - Requirements: REQ-008
  - Depends on: —
  - Done when: the precedence tests pass

- [x] TASK-004: `RegisterRecurring` resolves the ID, time zone and on/off (`Cron.Never`). `Trigger(Type)` uses the same ID resolver
  - Requirements: REQ-002, REQ-003, REQ-004, REQ-007, REQ-008
  - Depends on: TASK-001, TASK-003
  - Done when: `RecurringRegistrationTests` pass

- [x] TASK-005: Orphan cleanup only removes ExecutionFlow recurring jobs
  - Requirements: REQ-006
  - Depends on: TASK-004
  - Done when: the orphan tests pass

- [x] TASK-006: Stop registering `HangfireAutoRunFilter`, and mark it `[Obsolete]`
  - Requirements: REQ-003, REQ-004
  - Depends on: TASK-004
  - Done when: `Build()` doesn't register it (`HangfireSetupGlobalFiltersTests`)

- [x] TASK-007: README, spec, findings
  - Requirements: all
  - Depends on: TASK-001..TASK-006
  - Done when: the README documents `Id`, `TimeZone`, on/off per storage, queues and `[DisableConcurrentExecution]`, the spec is `approved`, and every AC names an existing test

## v1.3.0

- [x] TASK-008: `HangfireOptions.UsePlan` (registers the handlers and their auto-run) and job naming from the plan
  - Requirements: REQ-009
  - Depends on: execution-plan/TASK-002
  - Done when: `ExecutionPlanHangfireTests` pass

- [x] TASK-009: `PrerequisitesNotMetState`, `PrerequisiteGenerationStore`, `PrerequisiteGateFilter` and the increment in the dispatcher
  - Requirements: REQ-010
  - Depends on: TASK-008
  - Done when: `PrerequisiteGateTests` pass

- [x] TASK-010: `JobState.PrerequisitesNotMet`
  - Requirements: REQ-011
  - Depends on: —
  - Done when: the `ExecutionManagerTests` case passes

- [x] TASK-011: Run modes (`RunOnOwnSchedule`, `MinInterval`), no-overlap retrigger, and manual runs without the gate
  - Requirements: REQ-012
  - Depends on: TASK-009, execution-plan/TASK-003
  - Done when: the REQ-012 tests in `PrerequisiteGateTests`, `ExecutionPlanHangfireTests` and `RecurringRegistrationTests` pass
