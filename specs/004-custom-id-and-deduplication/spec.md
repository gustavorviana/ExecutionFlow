# Feature Specification: Custom ID and deduplication

**Feature Branch**: N/A (migrated)

**Created**: migrated from the custom SDD layout

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the brownfield as-is mapping (see [research.md](research.md), Findings). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/custom-id-and-deduplication/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept; `NFR-n` → `SC-n`).

**Packages**: `ExecutionFlow` (`ICustomIdEvent`, `DeduplicationBehavior`, `DeduplicationAttribute`, `FlowContext.SetCustomId`), `ExecutionFlow.Hangfire` (storage, lock, cleanup)

**Amends**: none.

## Context

Applications need to refer to background jobs by a business identifier (e.g. `payment-123`) instead of Hangfire's internal job ID. They use it to track, cancel or retry the job, and to avoid enqueueing the same work twice. The custom ID is stored with the job, and deduplication uses it to decide whether an equivalent job is still active.

Related capabilities:
- [003-dispatching](../003-dispatching/spec.md): the publish pipeline, `PublishResult`, and atomic creation of the custom ID (FR-007 there).
- [007-execution-manager](../007-execution-manager/spec.md): the full `IExecutionManager` API. This spec only covers how it matches jobs by custom ID.
- [006-lifecycle-hooks](../006-lifecycle-hooks/spec.md): the hooks receive the custom ID.

**Actors**: the producer app (publishes events that carry a custom ID, and chooses the deduplication behavior), the handler author (reads the custom ID while the job runs), and the operator / app code (looks up, cancels or retries jobs by custom ID).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Attach a business identifier to a job (Priority: P1)

As a producer app, I want to attach a business identifier to a job, so that I can find it later without keeping the Hangfire job ID.

**Why this priority**: the base every other story builds on.

**Independent Test**: publish an `ICustomIdEvent` and look it up with `IExecutionManager` by its custom ID.

**Acceptance Scenarios**: AC-001.1, AC-001.2, AC-003.1..AC-003.5.

---

### User Story 2 - Read the custom ID during execution (Priority: P3)

As a handler author, I want to read the custom ID while the job runs.

**Why this priority**: convenience for handlers; the job runs without it.

**Independent Test**: run a dispatched job with a custom ID and read `FlowContext.CustomId`.

**Acceptance Scenarios**: AC-002.1..AC-002.3.

---

### User Story 3 - Skip a second job for an active custom ID (Priority: P1)

As a producer app, I want to avoid a second job for the same custom ID while one is still active, so that the same work isn't done twice.

**Why this priority**: the main reason deduplication exists.

**Independent Test**: publish twice with `SkipIfExists` and the same custom ID, and check the second is skipped.

**Acceptance Scenarios**: AC-004.1..AC-004.3, AC-007.1..AC-007.4, AC-008.0..AC-008.3, AC-009.1, AC-009.2.

---

### User Story 4 - Replace the active job (Priority: P2)

As a producer app, I want a newer event to replace the active job with the same custom ID, so that only the latest version runs.

**Why this priority**: an alternative deduplication mode; skip covers the common case.

**Independent Test**: publish twice with `ReplaceExisting` and check the first job is deleted.

**Acceptance Scenarios**: AC-005.1..AC-005.3.

---

### User Story 5 - Deduplication behavior per event type (Priority: P2)

As a producer app, I want different event types to use different deduplication behaviors.

**Why this priority**: refines the global setting; not needed for a single behavior.

**Independent Test**: mark an event with `[Deduplication]` and check it overrides the global behavior.

**Acceptance Scenarios**: AC-006.1..AC-006.4.

### Edge Cases

- Storage without `JobStorageTransaction` (no hash expiry): the key isn't expired, and stale keys are still detected on read (AC-007.4).
- A consumer without the cleanup filter (an older version): keys remain until they expire, and are detected as stale on read.
- A custom ID equal to another job's Hangfire ID: `MatchesId` checks the Hangfire ID first, so that job matches. [INFERRED] Not tested.
- Reading the custom ID fails (storage error) during a lookup: the job is treated as having no custom ID, and a warning is logged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Custom ID from the event**: an event that implements `ICustomIdEvent` with a non-empty `CustomId` gets that value stored with its job, in the `CustomId` job parameter. *(Story 1)*
  - **AC-001.1**: **Given** an `ICustomIdEvent` with a non-empty `CustomId`, **When** it is published or scheduled, **Then** the value is stored as the job's custom ID. Tests: `DispatcherTests.Publish_SetsCustomId_WhenEventImplementsICustomIdEvent`, `Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2`
  - **AC-001.2**: **Given** an event that doesn't implement `ICustomIdEvent`, or whose `CustomId` is null or empty, **When** it is published, **Then** no custom ID is stored. Tests: `DispatcherTests.Publish_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent`, `Publish_TreatsEmptyCustomIdAsNone`

- **FR-002 Custom ID during execution**: the handler sees the publish-time custom ID as `FlowContext.CustomId`. `FlowContext<TEvent>.SetCustomId` is **obsolete** (compiler warning) and will be removed in 2.0. *(Story 2)*
  - **AC-002.1**: **Given** an event with a non-empty custom ID, **When** the job starts, **Then** it is exposed as `FlowContext.CustomId`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_SetsCustomId`
  - **AC-002.2**: **Given** a running handler, **When** it calls the obsolete `SetCustomId(id)`, **Then** `FlowContext.CustomId` and the stored custom ID are updated, but the deduplication reservation is NOT moved (RN-004). Test: `ContextTests.SetCustomId_Stores_Value_Readable_Via_CustomId` (in-memory part, see F-006 in [research.md](research.md))
  - **AC-002.3**: **Given** a running handler, **When** it calls `SetCustomId(null)`, **Then** the stored custom ID is cleared. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ClearsCustomId_WhenHandlerSetsNull`

- **FR-003 Find jobs by custom ID**: `IExecutionManager.IsRunning/IsPending/Cancel/Retry(string id)` accept either a Hangfire job ID or a custom ID. When a reservation key exists (FR-007), it's used first. Otherwise the Monitoring API is scanned, which covers jobs published without deduplication or before 1.2.0. *(Story 1)*
  - **AC-003.1**: **Given** a Processing job with that Hangfire ID or custom ID, **When** `IsRunning(id)` is called, **Then** it returns `true`. Tests: `ExecutionManagerTests.IsRunning_ReturnsTrue_WhenMatchingByHangfireJobId`, `IsRunning_ReturnsTrue_WhenMatchingByCustomId`, `IsRunning_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsRunning_FallsBackToScan_WhenReservedJobIsNotProcessing`
  - **AC-003.2**: **Given** a matching Enqueued job, **When** `IsPending(id)` is called, **Then** it returns `true`. (`IsPending` means "in a queue". Scheduled jobs aren't pending.) Tests: `ExecutionManagerTests.IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound`, `IsPending_ReturnsTrue_FromReservationKey_WithoutScanning`
  - **AC-003.3**: **Given** a null or empty ID, **When** a lookup is made, **Then** no job is matched. Tests: `ExecutionManagerTests.IsRunning_ReturnsFalse_WhenIdIsNullOrEmpty_EvenIfJobsWithoutCustomIdAreRunning`, `Cancel_DoesNothing_WhenIdIsNullOrEmpty`
  - **AC-003.4**: **Given** a reservation key pointing to an Enqueued or Processing job, **When** `Cancel(id)` is called, **Then** that job is deleted without scanning. Otherwise it acts on the first scan match: Processing jobs first, then each queue in order. Test: `ExecutionManagerTests.Cancel_DeletesReservedJob_WithoutScanning` (the first-match order is untested, see F-007 in [research.md](research.md))
  - **AC-003.5**: **Given** a stored custom ID written by v1.1.x (raw) or v1.2.0 (JSON), **When** a lookup is made, **Then** it matches the same way. Tests: `JobParametersTests.ReadCustomId_ReadsLegacyRawValue`, `ReadCustomId_ReadsJsonEncodedValue`

- **FR-004 Deduplication: skip if exists**: with the effective behavior `SkipIfExists` (FR-006), publishing an event whose non-empty custom ID has an **active** reserved job creates nothing. Active means Enqueued, Scheduled (including a failed job waiting for an automatic retry), Awaiting or Processing. *(Story 3)*
  - **AC-004.1**: **Given** `SkipIfExists` and an active reserved job for custom ID X, **When** an event with custom ID X is published or scheduled, **Then** no job is created and the result is `Enqueued = false`, `JobId = null`. Tests: `DispatcherTests.Publish_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_TimeSpan_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_DateTimeOffset_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Publish_SkipIfExists_ReturnsFalse_WhenReservedJobIsScheduled`
  - **AC-004.2**: **Given** `SkipIfExists` and no reservation, or a reserved job that is no longer active (Succeeded, Deleted, Failed), **When** the event is published, **Then** the job is created normally. Tests: `DispatcherTests.Publish_SkipIfExists_EnqueuesNormally_WhenNoExistingJob`, `Publish_SkipIfExists_Creates_WhenReservedJobIsNoLongerActive`
  - **AC-004.3**: **Given** an event with no custom ID, **When** it is published, **Then** deduplication is skipped. Tests: `DispatcherTests.Publish_SkipIfExists_IgnoresNonCustomIdEvents`, `Publish_TreatsEmptyCustomIdAsNone`

- **FR-005 Deduplication: replace existing**: with the effective behavior `ReplaceExisting`, publishing an event whose custom ID has an active reserved job deletes that job and creates the new one. *(Story 4)*
  - **AC-005.1**: **Given** `ReplaceExisting` and an active reserved job for custom ID X, **When** an event with custom ID X is published, **Then** that job is deleted (`IBackgroundJobClient.Delete`) and a new job is created and reserved. Tests: `DispatcherTests.Publish_ReplaceExisting_CancelsAndEnqueuesNew`, `Publish_ReplaceExisting_DeletesReservedJob`
  - **AC-005.2**: **Given** a replaced job that is already Processing, **When** it is replaced, **Then** it is marked Deleted without stopping the running handler. The handler stops only if it observes its `CancellationToken`, so the old and new job can run at the same time. Accepted as rule (F-002) and documented in the README. Test: none (Hangfire behavior; see F-002 in [research.md](research.md))
  - **AC-005.3**: **Given** an event with an empty custom ID, **When** it is published, **Then** no job is cancelled. Test: `DispatcherTests.Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty`

- **FR-006 Deduplication behavior per event type**: `[Deduplication(DeduplicationBehavior)]` on the event class (read from the event's runtime class, inherited by derived classes) overrides the global `ExecutionFlowOptions.DeduplicationBehavior`. `DeduplicationBehavior` and `DeduplicationAttribute` live in the core package. *(Story 5)*
  - **AC-006.1**: **Given** the global default `Disabled` and an event marked `[Deduplication(SkipIfExists)]`, **When** it is published with an active reserved job, **Then** it is skipped. Test: `DispatcherTests.Publish_UsesEventAttribute_OverGlobalBehavior`
  - **AC-006.2**: **Given** the global default `SkipIfExists` and an event marked `[Deduplication(Disabled)]`, **When** it is published, **Then** no lock is taken and the job is created. Test: `DispatcherTests.Publish_EventAttributeDisabled_OverridesGlobalSkipIfExists`
  - **AC-006.3**: **Given** an event class that derives from a class marked `[Deduplication]`, **When** it is published, **Then** the base class's behavior applies. Test: `DeduplicationAttributeTests.Attribute_IsInheritedByDerivedEvents`
  - **AC-006.4**: **Given** no configured behavior, **When** an event is published, **Then** `Disabled` is used. Test: `DeduplicationAttributeTests.ExecutionFlowOptions_DeduplicationBehavior_DefaultsToDisabled`

- **FR-007 Reservation key**: when deduplication applies (the effective behavior isn't `Disabled` and the custom ID is non-empty), the dispatcher checks and writes a reservation key `executionflow:dedup:{customId}` (a Hangfire hash holding the `JobId`) under a distributed lock `executionflow:dedup-lock:{customId}`. Nothing is locked or reserved otherwise (constitution principle VI). *(Story 3)*
  - **AC-007.1**: **Given** deduplication applies, **When** a job is created, **Then** the reservation key is written pointing to the new job, and set to expire after 30 days plus the schedule delay when the storage supports expiry. Tests: `DispatcherTests.Publish_WritesReservationKey_WhenDeduplicationEnabled`, `DeduplicationStoreTests.Reserve_WritesKeyAndSetsExpiration_WhenStorageSupportsIt`
  - **AC-007.2**: **Given** deduplication applies, **When** publishing, **Then** the lock is taken for that custom ID only, with `HangfireOptions.DeduplicationLockTimeout` (default 1s). Test: `DispatcherTests.Publish_AcquiresLockPerCustomId_WithConfiguredTimeout`
  - **AC-007.3**: **Given** deduplication doesn't apply, **When** publishing, **Then** no lock is taken and no key is written. Test: `DispatcherTests.Publish_DoesNotLockOrReserve_WhenDeduplicationDisabled`
  - **AC-007.4**: **Given** a key pointing to a job that isn't active, **When** it is read, **Then** it is treated as stale. Tests: `DeduplicationStoreTests.FindActiveJobId_ReturnsJobId_WhenReservedJobIsActive`, `FindActiveJobId_ReturnsNull_WhenKeyIsStale`, `FindActiveJobId_ReturnsNull_WhenNoKey`

- **FR-008 Reservation cleanup**: consumers (full `Build()`) register `DeduplicationCleanupFilter`, which releases the key in the same transaction as the state change. *(Story 3)*
  - **AC-008.0**: **Given** a setup, **When** `Build()` runs, **Then** `DeduplicationCleanupFilter` is registered once in `GlobalJobFilters`; `BuildDispatcherOnly()` does NOT register it (constitution principle IV). Tests: `HangfireSetupGlobalFiltersTests.Build_RegistersDeduplicationCleanupFilter_Once`, `BuildDispatcherOnly_DoesNotRegisterDeduplicationCleanupFilter`
  - **AC-008.1**: **Given** a key for the job's custom ID that points to it, **When** the job enters Succeeded, Deleted or Failed, **Then** the key is removed. Test: `DeduplicationCleanupFilterTests.OnStateApplied_RemovesKey_WhenJobLeavesActiveStates`
  - **AC-008.2**: **Given** a key that points to another job, or a job that is still active, **When** the state changes, **Then** the key is kept. Tests: `DeduplicationCleanupFilterTests.OnStateApplied_KeepsKey_WhenItPointsToAnotherJob`, `OnStateApplied_KeepsKey_WhenJobIsStillActive`, `DeduplicationStoreTests.Release_RemovesKey_OnlyWhenItPointsToTheJob`
  - **AC-008.3**: **Given** a storage failure, **When** releasing the key fails, **Then** a warning is logged and the state change never fails. Test: `DeduplicationCleanupFilterTests.OnStateApplied_DoesNotThrow_WhenStorageFails`

- **FR-009 Lock timeout**: behavior when the deduplication lock isn't acquired in time. *(Story 3)*
  - **AC-009.1**: **Given** the lock isn't acquired within `DeduplicationLockTimeout`, **When** publishing, **Then** `DistributedLockTimeoutException` is thrown and no job is created. Test: `DispatcherTests.Publish_Throws_WhenLockTimesOut`
  - **AC-009.2**: **Given** `HangfireOptions.CreateOnDeduplicationLockTimeout` = `true`, **When** the lock times out, **Then** the job is created without a reservation and a warning is logged. Test: `DispatcherTests.Publish_CreatesWithoutReservation_WhenLockTimesOutAndOptedIn`

### Business Rules

- **RN-001** [CONFIRMED] Custom IDs aren't unique. With `Disabled`, any number of jobs can share one custom ID.
- **RN-002** [CONFIRMED] The deduplication behavior is the event's `[Deduplication]` attribute if present, otherwise `ExecutionFlowOptions.DeduplicationBehavior` (FR-006).
- **RN-003** [CONFIRMED] An active job for deduplication is Enqueued, Scheduled, Awaiting or Processing (`DeduplicationStore`).
- **RN-004** [CONFIRMED] Only the custom ID given at publish time is reserved. A custom ID changed with the obsolete `SetCustomId` isn't deduplicated against, and doesn't move the key.
- **RN-005** [CONFIRMED] Guarantee: within one storage, two publishes of the same custom ID are serialized by the lock, so at most one active job is reserved. Known gap: a process crash between creating the job and writing the key can allow one duplicate. Handlers that must not run twice have to be idempotent.
- **RN-006** [CONFIRMED] Publishing with deduplication costs a lock plus O(1) storage operations. Lookups through the execution manager use the key first and fall back to an O(active jobs) scan.
- **RN-007** [CONFIRMED] The custom ID is passed to lifecycle hooks as `ExecutionEvent.CustomId`. Test: `StateFilterTests.CustomId_Passed_InEvent`
- **RN-008** [CONFIRMED] Jobs created before 1.2.0 have no reservation key, so deduplication doesn't see them. The execution manager still finds them through the scan.

### Key Entities

- **ICustomIdEvent**: event contract carrying `CustomId`.
- **DeduplicationBehavior / DeduplicationAttribute**: `Disabled`, `SkipIfExists`, `ReplaceExisting`; global or per event type.
- **Reservation key**: Hangfire hash `executionflow:dedup:{customId}` (`JobId`, `CreatedAt`), guarded by the lock `executionflow:dedup-lock:{customId}`.
- **DeduplicationStore / DeduplicationCleanupFilter** (internal): read, reserve and release the key.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001): with deduplication, a publish waits at most `DeduplicationLockTimeout` for the lock (constitution principle VI), and only publishes of the same custom ID contend.

## Assumptions

- Out of scope: uniqueness guarantees across different storages; deduplication of recurring jobs (they have a fixed ID per handler; see [005-recurring-jobs](../005-recurring-jobs/spec.md)); exactly-once execution.
- Breaking changes in 1.2.0:
  - `DeduplicationBehavior` moved from `ExecutionFlow.Hangfire` to `ExecutionFlow.Abstractions`, and the property moved from `HangfireOptions` to `ExecutionFlowOptions` (the base class, so `options.DeduplicationBehavior` still compiles). Code that names the enum with only `using ExecutionFlow.Hangfire;` must add `using ExecutionFlow.Abstractions;`. This is a source break in a minor version, by the maintainer's decision ([research.md](research.md) R-003).
  - `FlowContext.SetCustomId` is obsolete (a warning, and an error with `TreatWarningsAsErrors`).
  - Deduplication doesn't see jobs created before 1.2.0 (RN-008).
- Open questions: none.
