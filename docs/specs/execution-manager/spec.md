# Spec: Execution manager

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`IExecutionManager`, `JobInfo`, `JobState`, `JobStateSummary`), `ExecutionFlow.Hangfire` (`HangfireExecutionManager`)

## Context
Application code and admin screens need to ask "is this job running?", cancel or retry a job, and list or count jobs by state, without talking to Hangfire directly. `IExecutionManager` offers that over event jobs (by Hangfire ID or custom ID) and recurring jobs (by handler type).

Related capabilities:
- [custom-id-and-deduplication](../custom-id-and-deduplication/spec.md): matching by custom ID, and the reservation-key fast path (REQ-003 there).
- [recurring-jobs](../recurring-jobs/spec.md): recurring jobs addressed by handler type.
- [setup-and-configuration](../setup-and-configuration/spec.md): where `IExecutionManager` is registered.

## Actors
| Actor | Description |
| ----- | ----------- |
| App code | Checks, cancels or retries a specific job. |
| Admin / monitoring UI | Lists and counts jobs by state. |

## User stories
- US-001: As app code, I want to know whether a job is running or waiting, so that I can avoid duplicate work or show progress.
- US-002: As app code, I want to cancel a job that hasn't finished, including one scheduled for later, and know whether it worked.
- US-003: As app code, I want to retry a failed job.
- US-004: As an admin UI, I want to list and count jobs by state without loading everything into memory.

## Requirements

### REQ-001: Query a job's state by ID or handler
`IsRunning(string)` checks Processing jobs, and `IsPending(string)` checks Enqueued jobs, matching the Hangfire ID or the custom ID. The `Type` overloads do the same for recurring jobs by handler type.
Source: US-001

Acceptance criteria:
- AC-001.1: Tests: `ExecutionManagerTests.IsRunning_ReturnsTrue_WhenMatchingByHangfireJobId`, `IsRunning_ReturnsTrue_WhenMatchingByCustomId`, `IsRunning_ReturnsFalse_WhenNoMatchingJob`, `IsRunning_ReturnsFalse_WhenNoProcessingJobs`, `IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound`, `IsPending_ReturnsFalse_WhenNoMatchingJob`, `IsRunning_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsPending_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsRunning_FallsBackToScan_WhenReservedJobIsNotProcessing`, `IsRunning_ReturnsFalse_WhenIdIsNullOrEmpty_EvenIfJobsWithoutCustomIdAreRunning`
- AC-001.2: Tests: `ExecutionManagerTests.IsRunning_Type_ReturnsTrue_WhenMatchingRecurringJobProcessing`, `IsRunning_Type_ReturnsFalse_WhenDifferentHandlerType`, `IsRunning_Type_ReturnsFalse_WhenNoProcessingJobs`, `IsRunning_Type_ReturnsFalse_WhenJobIsEventNotRecurring`, `IsPending_Type_ReturnsTrue_WhenMatchingRecurringJobEnqueued`, `IsPending_Type_ReturnsFalse_WhenDifferentHandlerType`, `IsPending_Type_ReturnsFalse_WhenNoEnqueuedJobs`

### REQ-002: Cancel
`Cancel(string)` deletes an active job (Scheduled, Enqueued, Awaiting or Processing) matching the ID and returns `true`, or returns `false` when nothing matched. Lookup order: an exact Hangfire job ID (no scan), then the reservation key, then a scan of Processing, each queue, and Scheduled. `Cancel(Type)` does the same for a recurring handler, scanning Processing, Enqueued and Scheduled.
Source: US-002

Acceptance criteria:
- AC-002.1: WHEN a Processing or Enqueued job matches, THE SYSTEM SHALL delete it. Tests: `ExecutionManagerTests.Cancel_QueriesProcessingJobs`, `Cancel_DeletesReservedJob_WithoutScanning`, `Cancel_Type_DeletesProcessingRecurringJob`, `Cancel_Type_DeletesEnqueuedRecurringJob_WhenNotProcessing`
- AC-002.2: WHEN the ID is null or empty, or nothing matches, THE SYSTEM SHALL delete nothing and return `false`. Tests: `ExecutionManagerTests.Cancel_DoesNothing_WhenIdIsNullOrEmpty`, `Cancel_ReturnsFalse_WhenNothingMatches`, `Cancel_Type_DoesNothing_WhenNoMatchingJob`
- AC-002.3: WHEN the ID is the exact Hangfire ID of an active job, including a Scheduled one, THE SYSTEM SHALL delete it without scanning and return `true`. Test: `ExecutionManagerTests.Cancel_DeletesExactHangfireId_WithoutScanning_EvenWhenScheduled`
- AC-002.4: WHEN a Scheduled job matches by custom ID, THE SYSTEM SHALL delete it. Test: `ExecutionManagerTests.Cancel_FindsScheduledJob_ByCustomId`
- AC-002.5: WHEN a recurring job is Scheduled (waiting for a retry), `Cancel(Type)` SHALL delete it. Test: `ExecutionManagerTests.Cancel_Type_FindsScheduledRecurringJob`

### REQ-003: Retry
`Retry(string)` requeues the first Failed job matching the ID. `Retry(Type)` does the same for a recurring handler. Both return whether a job was requeued.
Source: US-003

Acceptance criteria:
- AC-003.1: Tests: `ExecutionManagerTests.Retry_RequeuesFailedJob_ReturnsTrue`, `Retry_ReturnsFalse_WhenNoFailedJobWithCustomId`, `Retry_ReturnsFalse_WhenCustomIdDoesNotMatch`, `Retry_Type_RequeuesFailedRecurringJob_ReturnsTrue`, `Retry_Type_ReturnsFalse_WhenNoFailedRecurringJob`, `Retry_Type_ReturnsFalse_WhenDifferentHandlerType`, `Retry_Type_ReturnsFalse_WhenFailedJobIsEventNotRecurring`

### REQ-004: List jobs by state
`GetJobs(JobState)` lazily enumerates the ExecutionFlow jobs in a state. `JobState` is Enqueued, Processing, Succeeded, Failed, Cancelled (Hangfire's Deleted) and Scheduled.
Source: US-004

Acceptance criteria:
- AC-004.1: Tests: `ExecutionManagerTests.GetJobs_Processing_ReturnsJobsWithCorrectJobInfo`, `GetJobs_Enqueued_QueriesEnqueuedJobs`, `GetJobs_Failed_QueriesFailedJobs`, `GetJobs_Cancelled_QueriesDeletedJobs`, `GetJobs_Succeeded_QueriesSucceededJobs`, `GetJobs_Scheduled_ListsScheduledJobs`, `GetJobs_ReturnsEmpty_WhenNoJobsExist`, `GetJobs_CustomIdIsNull_WhenNoCustomIdParameter`, `GetJobs_StateChangedAt_PopulatedCorrectly`
- AC-004.2: WHEN the caller consumes only part of the sequence, THE SYSTEM SHALL read only the storage pages needed. Nothing is read before enumeration starts. Test: `ExecutionManagerTests.GetJobs_IsLazy_AndReadsOnlyThePagesConsumed`
- AC-004.3: THE SYSTEM SHALL return only ExecutionFlow jobs: loaded jobs running `HangfireJobDispatcher`, and unloadable jobs whose invocation data names `HangfireJobDispatcher`. Tests: `ExecutionManagerTests.GetJobs_ExcludesNonExecutionFlowJobs_LoadedOrNot`, `GetJobs_EventTypeIsNull_WhenJobIsNull`
- AC-004.4: FOR recurring jobs, THE SYSTEM SHALL set `HandlerType`, and SHALL leave `EventType`/`EventTypeName` null. FOR unloadable jobs, all three are null. Tests: `ExecutionManagerTests.GetJobs_Recurring_HasHandlerType_AndNoEventType`, `GetJobs_EventTypeIsNull_WhenJobIsNull`
- AC-004.5: THE SYSTEM SHALL treat timestamps with `Kind = Unspecified` as UTC. Test: `ExecutionManagerTests.GetJobs_TreatsUnspecifiedTimestampsAsUtc` (it only catches a regression on a host outside UTC)

### REQ-005: Count jobs
`CountJobs(JobState)` and `GetStateSummary()` read Hangfire's storage-wide statistics, including Scheduled.
Source: US-004

Acceptance criteria:
- AC-005.1: Tests: `ExecutionManagerTests.CountJobs_ReturnsCorrectCount`, `CountJobs_ReturnsZero_WhenNoJobs`, `GetStateSummary_ReturnsAllCounts`, `GetStateSummary_ReturnsZeros_WhenNoJobs`, `CountJobs_And_Summary_IncludeScheduled`
- AC-005.2: The counts cover the whole storage, including non-ExecutionFlow jobs, so they can exceed what `GetJobs` returns. Documented.

## Business rules
- RN-001 [CONFIRMED] Matching by ID checks the Hangfire ID first, then the custom ID.
- RN-002 [CONFIRMED] With several jobs sharing a custom ID, `Cancel`/`Retry` act on the first match.
- RN-003 [CONFIRMED] `GetJobs` is lazy: enumerating twice queries twice, the connection stays open during enumeration, and the result isn't a snapshot. Documented in the XML docs and README.
- RN-004 [CONFIRMED] `IExecutionManager` is registered by full `Build()` and by `AddHangfireToExecutionFlow`.

## Edge cases and errors
- Reading a custom ID fails: the job is treated as having none, and a warning is logged.
- Reading the state of an ID that isn't a valid Hangfire job ID throws on some storages: it's treated as "not a job ID", a warning is logged, and the lookup continues with the key and the scan.

## Non-functional requirements
- NFR-001: `GetJobs` memory use is bounded by the internal page size (10) plus what the caller keeps.
- NFR-002: Cancelling by exact Hangfire ID or by reservation key costs O(1) storage reads. Otherwise it's O(active jobs).

## Out of scope
- Awaiting jobs in `JobState` (continuations aren't exposed).
- Changing a job's schedule.

## Breaking and behavior changes in 1.2.0
- `IExecutionManager.Cancel(string)` and `Cancel(Type)` return `bool`. This breaks implementers of the interface.
- `JobState.Scheduled` was appended, so `switch` statements over `JobState` gain a case.
- `GetJobs` is lazy and excludes non-ExecutionFlow jobs.
- For recurring jobs, `JobInfo.EventTypeName` is null (it used to be `"DispatchRecurringAsync"`), and `HandlerType` is set.

## Open questions
- None.
