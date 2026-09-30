# Tasks: Dispatching v1.2.0

Plan: [plan.md](plan.md)

- [x] TASK-001: Refactor `Publish`/`Schedule` into a single `Dispatch<TEvent>(TEvent, IState)` pipeline
  - Requirements: REQ-001, REQ-002, REQ-003 (behavior unchanged)
  - Depends on: —
  - Done when: the existing `DispatcherTests` pass without changes

- [x] TASK-002: Reject null events
  - Requirements: AC-001.3, AC-002.2, AC-003.2
  - Depends on: TASK-001
  - Done when: `*_ThrowsArgumentNullException_WhenEventIsNull` pass for all 3 methods

- [x] TASK-003: Treat null or empty `CustomId` as "no custom ID", and guard `HangfireExecutionManager` lookups
  - Requirements: AC-004.3, AC-006.5 (fixes F-001, F-009)
  - Depends on: TASK-001
  - Done when: `Publish_TreatsEmptyCustomIdAsNone` and `Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty` pass

- [x] TASK-004: Spike on how `IBackgroundJobClientV2.Create` encodes parameter values
  - Requirements: REQ-007
  - Depends on: —
  - Done when: the encoding is known and recorded in DEC-004

- [x] TASK-005: Atomic job creation with `CustomId`, with a fallback for non-V2 clients and legacy-tolerant reads
  - Requirements: AC-007.1, AC-007.2, AC-007.3
  - Depends on: TASK-001, TASK-003, TASK-004
  - Done when: `Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2` and `JobParametersTests.ReadCustomId_ReadsLegacyRawValue` pass, and existing custom ID tests pass

- [x] TASK-006: `HangfireOptions.RetryUnregisteredEventJobs` and disabling retries in `HandlerJobFilterProvider`
  - Requirements: AC-008.1, AC-008.2
  - Depends on: —
  - Done when: `UnregisteredEventJob_GetsRetryDisabled_ByDefault` and `UnregisteredEventJob_KeepsDefaultRetries_WhenOptionEnabled` pass

- [x] TASK-007: Tests for criteria with no coverage
  - Requirements: AC-004.4 (Schedule), AC-005.1, AC-005.2, AC-006.4, RN-001
  - Depends on: TASK-001
  - Done when: every AC in the spec names a test that exists

- [x] TASK-008: README, XML docs, version 1.2.0, and spec status
  - Requirements: all
  - Depends on: TASK-001..TASK-007
  - Done when: the README documents RN-001 and the new option, `Src/Directory.Build.props` is `1.2.0`, and the spec is `approved`
