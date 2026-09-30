# Spec: Custom ID and deduplication

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`ICustomIdEvent`, `DeduplicationBehavior`, `DeduplicationAttribute`, `FlowContext.SetCustomId`), `ExecutionFlow.Hangfire` (storage, lock, cleanup)

## Context
Applications need to refer to background jobs by a business identifier (e.g. `payment-123`) instead of Hangfire's internal job ID. They use it to track, cancel or retry the job, and to avoid enqueueing the same work twice. The custom ID is stored with the job, and deduplication uses it to decide whether an equivalent job is still active.

Related capabilities:
- [dispatching](../dispatching/spec.md): the publish pipeline, `PublishResult`, and atomic creation of the custom ID (REQ-007 there).
- [execution-manager](../execution-manager/spec.md): the full `IExecutionManager` API. This spec only covers how it matches jobs by custom ID.
- [lifecycle-hooks](../lifecycle-hooks/spec.md): the hooks receive the custom ID.

## Actors
| Actor | Description |
| ----- | ----------- |
| Producer app | Publishes events that carry a custom ID, and chooses the deduplication behavior. |
| Handler author | Reads the custom ID while the job runs. |
| Operator / app code | Looks up, cancels or retries jobs by custom ID. |

## User stories
- US-001: As a producer app, I want to attach a business identifier to a job, so that I can find it later without keeping the Hangfire job ID.
- US-002: As a handler author, I want to read the custom ID while the job runs.
- US-003: As a producer app, I want to avoid a second job for the same custom ID while one is still active, so that the same work isn't done twice.
- US-004: As a producer app, I want a newer event to replace the active job with the same custom ID, so that only the latest version runs.
- US-005: As a producer app, I want different event types to use different deduplication behaviors.

## Requirements

### REQ-001: Custom ID from the event
An event that implements `ICustomIdEvent` with a non-empty `CustomId` gets that value stored with its job, in the `CustomId` job parameter.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN an `ICustomIdEvent` with a non-empty `CustomId` is published or scheduled, THE SYSTEM SHALL store the value as the job's custom ID. Tests: `DispatcherTests.Publish_SetsCustomId_WhenEventImplementsICustomIdEvent`, `Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2`
- AC-001.2: WHEN the event doesn't implement `ICustomIdEvent`, or its `CustomId` is null or empty, THE SYSTEM SHALL store no custom ID. Tests: `DispatcherTests.Publish_DoesNotSetCustomId_WhenEventDoesNotImplementICustomIdEvent`, `Publish_TreatsEmptyCustomIdAsNone`

### REQ-002: Custom ID during execution
The handler sees the publish-time custom ID as `FlowContext.CustomId`. `FlowContext<TEvent>.SetCustomId` is **obsolete** (compiler warning) and will be removed in 2.0.
Source: US-002

Acceptance criteria:
- AC-002.1: WHEN the job starts and the event has a non-empty custom ID, THE SYSTEM SHALL expose it as `FlowContext.CustomId`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_SetsCustomId`
- AC-002.2: WHEN the handler calls the obsolete `SetCustomId(id)`, THE SYSTEM SHALL update `FlowContext.CustomId` and the stored custom ID, but SHALL NOT move the deduplication reservation (RN-004). Test: `ContextTests.SetCustomId_Stores_Value_Readable_Via_CustomId` (in-memory part, see F-006)
- AC-002.3: WHEN the handler calls `SetCustomId(null)`, THE SYSTEM SHALL clear the stored custom ID. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ClearsCustomId_WhenHandlerSetsNull`

### REQ-003: Find jobs by custom ID
`IExecutionManager.IsRunning/IsPending/Cancel/Retry(string id)` accept either a Hangfire job ID or a custom ID. When a reservation key exists (REQ-007), it's used first. Otherwise the Monitoring API is scanned, which covers jobs published without deduplication or before 1.2.0.
Source: US-001

Acceptance criteria:
- AC-003.1: WHEN `IsRunning(id)` is called and a Processing job has that Hangfire ID or custom ID, THE SYSTEM SHALL return `true`. Tests: `ExecutionManagerTests.IsRunning_ReturnsTrue_WhenMatchingByHangfireJobId`, `IsRunning_ReturnsTrue_WhenMatchingByCustomId`, `IsRunning_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsRunning_FallsBackToScan_WhenReservedJobIsNotProcessing`
- AC-003.2: WHEN `IsPending(id)` is called and an Enqueued job matches, THE SYSTEM SHALL return `true`. (`IsPending` means "in a queue". Scheduled jobs aren't pending.) Tests: `ExecutionManagerTests.IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound`, `IsPending_ReturnsTrue_FromReservationKey_WithoutScanning`
- AC-003.3: WHEN the ID is null or empty, THE SYSTEM SHALL match no job. Tests: `ExecutionManagerTests.IsRunning_ReturnsFalse_WhenIdIsNullOrEmpty_EvenIfJobsWithoutCustomIdAreRunning`, `Cancel_DoesNothing_WhenIdIsNullOrEmpty`
- AC-003.4: WHEN `Cancel(id)` is called and the reservation key points to an Enqueued or Processing job, THE SYSTEM SHALL delete that job without scanning. Otherwise it SHALL act on the first scan match: Processing jobs first, then each queue in order. Test: `ExecutionManagerTests.Cancel_DeletesReservedJob_WithoutScanning` (the first-match order is untested, see F-007)
- AC-003.5: WHEN a stored custom ID was written by v1.1.x (raw) or v1.2.0 (JSON), THE SYSTEM SHALL match it the same way. Tests: `JobParametersTests.ReadCustomId_ReadsLegacyRawValue`, `ReadCustomId_ReadsJsonEncodedValue`

### REQ-004: Deduplication: skip if exists
With the effective behavior `SkipIfExists` (REQ-006), publishing an event whose non-empty custom ID has an **active** reserved job creates nothing. Active means Enqueued, Scheduled (including a failed job waiting for an automatic retry), Awaiting or Processing.
Source: US-003

Acceptance criteria:
- AC-004.1: Given `SkipIfExists` and an active reserved job for custom ID X, When an event with custom ID X is published or scheduled, Then no job is created and the result is `Enqueued = false`, `JobId = null`. Tests: `DispatcherTests.Publish_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_TimeSpan_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_DateTimeOffset_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Publish_SkipIfExists_ReturnsFalse_WhenReservedJobIsScheduled`
- AC-004.2: Given `SkipIfExists` and no reservation, or a reserved job that is no longer active (Succeeded, Deleted, Failed), When the event is published, Then the job is created normally. Tests: `DispatcherTests.Publish_SkipIfExists_EnqueuesNormally_WhenNoExistingJob`, `Publish_SkipIfExists_Creates_WhenReservedJobIsNoLongerActive`
- AC-004.3: WHEN the event has no custom ID, THE SYSTEM SHALL skip deduplication. Tests: `DispatcherTests.Publish_SkipIfExists_IgnoresNonCustomIdEvents`, `Publish_TreatsEmptyCustomIdAsNone`

### REQ-005: Deduplication: replace existing
With the effective behavior `ReplaceExisting`, publishing an event whose custom ID has an active reserved job deletes that job and creates the new one.
Source: US-004

Acceptance criteria:
- AC-005.1: Given `ReplaceExisting` and an active reserved job for custom ID X, When an event with custom ID X is published, Then that job is deleted (`IBackgroundJobClient.Delete`) and a new job is created and reserved. Tests: `DispatcherTests.Publish_ReplaceExisting_CancelsAndEnqueuesNew`, `Publish_ReplaceExisting_DeletesReservedJob`
- AC-005.2: WHEN the replaced job is already Processing, THE SYSTEM SHALL mark it Deleted without stopping the running handler. The handler stops only if it observes its `CancellationToken`, so the old and new job can run at the same time. Accepted as rule (F-002) and documented in the README. Test: none (Hangfire behavior)
- AC-005.3: WHEN the event has an empty custom ID, THE SYSTEM SHALL NOT cancel any job. Test: `DispatcherTests.Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty`

### REQ-006: Deduplication behavior per event type
`[Deduplication(DeduplicationBehavior)]` on the event class (read from the event's runtime class, inherited by derived classes) overrides the global `ExecutionFlowOptions.DeduplicationBehavior`. `DeduplicationBehavior` and `DeduplicationAttribute` live in the core package.
Source: US-005

Acceptance criteria:
- AC-006.1: Given the global default `Disabled` and an event marked `[Deduplication(SkipIfExists)]`, When it is published with an active reserved job, Then it is skipped. Test: `DispatcherTests.Publish_UsesEventAttribute_OverGlobalBehavior`
- AC-006.2: Given the global default `SkipIfExists` and an event marked `[Deduplication(Disabled)]`, When it is published, Then no lock is taken and the job is created. Test: `DispatcherTests.Publish_EventAttributeDisabled_OverridesGlobalSkipIfExists`
- AC-006.3: WHEN an event class derives from a class marked `[Deduplication]`, THE SYSTEM SHALL apply the base class's behavior. Test: `DeduplicationAttributeTests.Attribute_IsInheritedByDerivedEvents`
- AC-006.4: WHEN no behavior is configured, THE SYSTEM SHALL use `Disabled`. Test: `DeduplicationAttributeTests.ExecutionFlowOptions_DeduplicationBehavior_DefaultsToDisabled`

### REQ-007: Reservation key
When deduplication applies (the effective behavior isn't `Disabled` and the custom ID is non-empty), the dispatcher checks and writes a reservation key `executionflow:dedup:{customId}` (a Hangfire hash holding the `JobId`) under a distributed lock `executionflow:dedup-lock:{customId}`. Nothing is locked or reserved otherwise (P-007).
Source: US-003

Acceptance criteria:
- AC-007.1: WHEN a job is created with deduplication, THE SYSTEM SHALL write the reservation key pointing to the new job, and SHALL set it to expire after 30 days plus the schedule delay when the storage supports expiry. Tests: `DispatcherTests.Publish_WritesReservationKey_WhenDeduplicationEnabled`, `DeduplicationStoreTests.Reserve_WritesKeyAndSetsExpiration_WhenStorageSupportsIt`
- AC-007.2: WHEN deduplication applies, THE SYSTEM SHALL take the lock for that custom ID only, with `HangfireOptions.DeduplicationLockTimeout` (default 1s). Test: `DispatcherTests.Publish_AcquiresLockPerCustomId_WithConfiguredTimeout`
- AC-007.3: WHEN deduplication doesn't apply, THE SYSTEM SHALL take no lock and write no key. Test: `DispatcherTests.Publish_DoesNotLockOrReserve_WhenDeduplicationDisabled`
- AC-007.4: WHEN the key points to a job that isn't active, THE SYSTEM SHALL treat it as stale. Tests: `DeduplicationStoreTests.FindActiveJobId_ReturnsJobId_WhenReservedJobIsActive`, `FindActiveJobId_ReturnsNull_WhenKeyIsStale`, `FindActiveJobId_ReturnsNull_WhenNoKey`

### REQ-008: Reservation cleanup
Consumers (full `Build()`) register `DeduplicationCleanupFilter`, which releases the key in the same transaction as the state change.
Source: US-003

Acceptance criteria:
- AC-008.0: WHEN `Build()` runs, THE SYSTEM SHALL register `DeduplicationCleanupFilter` once in `GlobalJobFilters`. `BuildDispatcherOnly()` SHALL NOT register it (P-005). Tests: `HangfireSetupGlobalFiltersTests.Build_RegistersDeduplicationCleanupFilter_Once`, `BuildDispatcherOnly_DoesNotRegisterDeduplicationCleanupFilter`
- AC-008.1: WHEN a job enters Succeeded, Deleted or Failed and the key for its custom ID points to it, THE SYSTEM SHALL remove the key. Test: `DeduplicationCleanupFilterTests.OnStateApplied_RemovesKey_WhenJobLeavesActiveStates`
- AC-008.2: WHEN the key points to another job, or the job is still active, THE SYSTEM SHALL keep the key. Tests: `DeduplicationCleanupFilterTests.OnStateApplied_KeepsKey_WhenItPointsToAnotherJob`, `OnStateApplied_KeepsKey_WhenJobIsStillActive`, `DeduplicationStoreTests.Release_RemovesKey_OnlyWhenItPointsToTheJob`
- AC-008.3: WHEN releasing the key fails, THE SYSTEM SHALL log a warning and never fail the state change. Test: `DeduplicationCleanupFilterTests.OnStateApplied_DoesNotThrow_WhenStorageFails`

### REQ-009: Lock timeout
Source: US-003

Acceptance criteria:
- AC-009.1: WHEN the lock isn't acquired within `DeduplicationLockTimeout`, THE SYSTEM SHALL throw `DistributedLockTimeoutException` and create no job. Test: `DispatcherTests.Publish_Throws_WhenLockTimesOut`
- AC-009.2: WHEN `HangfireOptions.CreateOnDeduplicationLockTimeout` is `true` and the lock times out, THE SYSTEM SHALL create the job without a reservation and log a warning. Test: `DispatcherTests.Publish_CreatesWithoutReservation_WhenLockTimesOutAndOptedIn`

## Business rules
- RN-001 [CONFIRMED] Custom IDs aren't unique. With `Disabled`, any number of jobs can share one custom ID.
- RN-002 [CONFIRMED] The deduplication behavior is the event's `[Deduplication]` attribute if present, otherwise `ExecutionFlowOptions.DeduplicationBehavior` (REQ-006).
- RN-003 [CONFIRMED] An active job for deduplication is Enqueued, Scheduled, Awaiting or Processing (`DeduplicationStore`).
- RN-004 [CONFIRMED] Only the custom ID given at publish time is reserved. A custom ID changed with the obsolete `SetCustomId` isn't deduplicated against, and doesn't move the key.
- RN-005 [CONFIRMED] Guarantee: within one storage, two publishes of the same custom ID are serialized by the lock, so at most one active job is reserved. Known gap: a process crash between creating the job and writing the key can allow one duplicate. Handlers that must not run twice have to be idempotent.
- RN-006 [CONFIRMED] Publishing with deduplication costs a lock plus O(1) storage operations. Lookups through the execution manager use the key first and fall back to an O(active jobs) scan.
- RN-007 [CONFIRMED] The custom ID is passed to lifecycle hooks as `ExecutionEvent.CustomId`. Test: `StateFilterTests.CustomId_Passed_InEvent`
- RN-008 [CONFIRMED] Jobs created before 1.2.0 have no reservation key, so deduplication doesn't see them. The execution manager still finds them through the scan.

## Edge cases and errors
- Storage without `JobStorageTransaction` (no hash expiry): the key isn't expired, and stale keys are still detected on read (AC-007.4).
- A consumer without the cleanup filter (an older version): keys remain until they expire, and are detected as stale on read.
- A custom ID equal to another job's Hangfire ID: `MatchesId` checks the Hangfire ID first, so that job matches. [INFERRED] Not tested.
- Reading the custom ID fails (storage error) during a lookup: the job is treated as having no custom ID, and a warning is logged.

## Non-functional requirements
- NFR-001: With deduplication, a publish waits at most `DeduplicationLockTimeout` for the lock (P-007), and only publishes of the same custom ID contend.

## Out of scope
- Uniqueness guarantees across different storages.
- Deduplication of recurring jobs (they have a fixed ID per handler; see [recurring-jobs](../recurring-jobs/spec.md)).
- Exactly-once execution.

## Breaking changes in 1.2.0
- `DeduplicationBehavior` moved from `ExecutionFlow.Hangfire` to `ExecutionFlow.Abstractions`, and the property moved from `HangfireOptions` to `ExecutionFlowOptions` (the base class, so `options.DeduplicationBehavior` still compiles). Code that names the enum with only `using ExecutionFlow.Hangfire;` must add `using ExecutionFlow.Abstractions;`. This is a source break in a minor version, by the maintainer's decision (plan DEC-003).
- `FlowContext.SetCustomId` is obsolete (a warning, and an error with `TreatWarningsAsErrors`).
- Deduplication doesn't see jobs created before 1.2.0 (RN-008).

## Open questions
- None.
