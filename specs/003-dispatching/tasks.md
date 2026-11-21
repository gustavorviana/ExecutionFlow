---
description: "Tasks for dispatching v1.2.0 (migrated; all complete)"
---

# Tasks: Dispatching v1.2.0

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Foundational

- [x] T001 [US1] Refactor `Publish`/`Schedule` into a single `Dispatch<TEvent>(TEvent, IState)` pipeline in `Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs` (FR-001, FR-002, FR-003, behavior unchanged; depends on: none; done when the existing `DispatcherTests` pass without changes)
- [x] T004 [P] [US3] Spike on how `IBackgroundJobClientV2.Create` encodes parameter values (FR-007; depends on: none; done when the encoding is known and recorded in [research.md](research.md) R-004)

## Phase 2: User Stories 1, 2 - Publish and schedule (Priority: P1)

- [x] T002 [US1] Reject null events (AC-001.3, AC-002.2, AC-003.2; depends on T001; done when `*_ThrowsArgumentNullException_WhenEventIsNull` pass for all 3 methods)

## Phase 3: User Story 3 - Job identifier (Priority: P2)

- [x] T003 [US3] Treat null or empty `CustomId` as "no custom ID", and guard `HangfireExecutionManager` lookups (AC-004.3, AC-006.5; fixes F-001, F-009; depends on T001; done when `Publish_TreatsEmptyCustomIdAsNone` and `Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty` pass)
- [x] T005 [US3] Atomic job creation with `CustomId`, with a fallback for non-V2 clients and legacy-tolerant reads (AC-007.1, AC-007.2, AC-007.3; depends on T001, T003, T004; done when `Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2` and `JobParametersTests.ReadCustomId_ReadsLegacyRawValue` pass, and existing custom ID tests pass)

## Phase 4: User Story 4 - Handler execution (Priority: P1)

- [x] T006 [P] [US4] `HangfireOptions.RetryUnregisteredEventJobs` and disabling retries in `HandlerJobFilterProvider` (AC-008.1, AC-008.2; depends on: none; done when `UnregisteredEventJob_GetsRetryDisabled_ByDefault` and `UnregisteredEventJob_KeepsDefaultRetries_WhenOptionEnabled` pass)

## Phase 5: Polish & Cross-Cutting

- [x] T007 Tests for criteria with no coverage (AC-004.4 (Schedule), AC-005.1, AC-005.2, AC-006.4, RN-001; depends on T001; done when every AC in the spec names a test that exists)
- [x] T008 README, XML docs, version 1.2.0, and spec status (all requirements; depends on T001..T007; done when the README documents RN-001 and the new option, `Src/Directory.Build.props` is `1.2.0`, and the spec is `approved`)
