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
