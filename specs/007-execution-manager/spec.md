# Feature Specification: Execution manager

**Feature Branch**: N/A (migrated)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the as-is mapping (see the findings in [research.md](research.md)). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/execution-manager/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` and `NFR` numbers kept).

**Packages**: `ExecutionFlow` (`IExecutionManager`, `JobInfo`, `JobState`, `JobStateSummary`), `ExecutionFlow.Hangfire` (`HangfireExecutionManager`)

**Amends**: none

## Context

Application code and admin screens need to ask "is this job running?", cancel or retry a job, and list or count jobs by state, without talking to Hangfire directly. `IExecutionManager` offers that over event jobs (by Hangfire ID or custom ID) and recurring jobs (by handler type).

Related capabilities:
- [004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md): matching by custom ID, and the reservation-key fast path (FR-003 there).
- [005-recurring-jobs](../005-recurring-jobs/spec.md): recurring jobs addressed by handler type.
- [002-setup-and-configuration](../002-setup-and-configuration/spec.md): where `IExecutionManager` is registered.

**Actors**:
- **App code**: checks, cancels or retries a specific job.
- **Admin / monitoring UI**: lists and counts jobs by state.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Know whether a job is running or waiting (Priority: P1)

As app code, I want to know whether a job is running or waiting, so that I can avoid duplicate work or show progress.

**Why this priority**: the most common query; avoiding duplicate work depends on it.

**Independent Test**: enqueue or process a job and call `IsRunning`/`IsPending` by Hangfire ID, custom ID and handler type.

**Acceptance Scenarios**: AC-001.1, AC-001.2.

---

### User Story 2 - Cancel a job that hasn't finished (Priority: P1)

As app code, I want to cancel a job that hasn't finished, including one scheduled for later, and know whether it worked.

**Why this priority**: cancelling a scheduled job is the most common reason to cancel ([research.md](research.md) F-001).

**Independent Test**: schedule a job, cancel it by exact ID and by custom ID, and check the result.

**Acceptance Scenarios**: AC-002.1..AC-002.5.

---

### User Story 3 - Retry a failed job (Priority: P2)

As app code, I want to retry a failed job.

**Why this priority**: operational control; failed jobs can also be retried from the dashboard.

**Independent Test**: fail a job and call `Retry` by ID and by handler type.

**Acceptance Scenarios**: AC-003.1.

---

### User Story 4 - List and count jobs by state (Priority: P2)

As an admin UI, I want to list and count jobs by state without loading everything into memory.

**Why this priority**: monitoring; not needed to run jobs.

**Independent Test**: call `GetJobs`, `CountJobs` and `GetStateSummary` for each `JobState`.

**Acceptance Scenarios**: AC-004.1..AC-004.5, AC-005.1, AC-005.2.

### Edge Cases

- Reading a custom ID fails: the job is treated as having none, and a warning is logged.
- Reading the state of an ID that isn't a valid Hangfire job ID throws on some storages: it's treated as "not a job ID", a warning is logged, and the lookup continues with the key and the scan.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Query a job's state by ID or handler**: `IsRunning(string)` checks Processing jobs, and `IsPending(string)` checks Enqueued jobs, matching the Hangfire ID or the custom ID. The `Type` overloads do the same for recurring jobs by handler type. *(Story 1)*
  - **AC-001.1**: **Given** jobs that match by Hangfire ID, custom ID or reservation key, **When** `IsRunning(string)`/`IsPending(string)` is called, **Then** it reports whether a matching Processing/Enqueued job exists, and a null or empty ID never matches. Tests: `ExecutionManagerTests.IsRunning_ReturnsTrue_WhenMatchingByHangfireJobId`, `IsRunning_ReturnsTrue_WhenMatchingByCustomId`, `IsRunning_ReturnsFalse_WhenNoMatchingJob`, `IsRunning_ReturnsFalse_WhenNoProcessingJobs`, `IsPending_ReturnsTrue_WhenMatchingEnqueuedJobFound`, `IsPending_ReturnsFalse_WhenNoMatchingJob`, `IsRunning_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsPending_ReturnsTrue_FromReservationKey_WithoutScanning`, `IsRunning_FallsBackToScan_WhenReservedJobIsNotProcessing`, `IsRunning_ReturnsFalse_WhenIdIsNullOrEmpty_EvenIfJobsWithoutCustomIdAreRunning`
  - **AC-001.2**: **Given** recurring jobs, **When** `IsRunning(Type)`/`IsPending(Type)` is called, **Then** only recurring jobs of that handler type match. Tests: `ExecutionManagerTests.IsRunning_Type_ReturnsTrue_WhenMatchingRecurringJobProcessing`, `IsRunning_Type_ReturnsFalse_WhenDifferentHandlerType`, `IsRunning_Type_ReturnsFalse_WhenNoProcessingJobs`, `IsRunning_Type_ReturnsFalse_WhenJobIsEventNotRecurring`, `IsPending_Type_ReturnsTrue_WhenMatchingRecurringJobEnqueued`, `IsPending_Type_ReturnsFalse_WhenDifferentHandlerType`, `IsPending_Type_ReturnsFalse_WhenNoEnqueuedJobs`

- **FR-002 Cancel**: `Cancel(string)` deletes an active job (Scheduled, Enqueued, Awaiting or Processing) matching the ID and returns `true`, or returns `false` when nothing matched. Lookup order: an exact Hangfire job ID (no scan), then the reservation key, then a scan of Processing, each queue, and Scheduled. `Cancel(Type)` does the same for a recurring handler, scanning Processing, Enqueued and Scheduled. *(Story 2)*
  - **AC-002.1**: **Given** a Processing or Enqueued job that matches, **When** `Cancel` is called, **Then** it is deleted. Tests: `ExecutionManagerTests.Cancel_QueriesProcessingJobs`, `Cancel_DeletesReservedJob_WithoutScanning`, `Cancel_Type_DeletesProcessingRecurringJob`, `Cancel_Type_DeletesEnqueuedRecurringJob_WhenNotProcessing`
  - **AC-002.2**: **Given** a null or empty ID, or nothing that matches, **When** `Cancel` is called, **Then** nothing is deleted and it returns `false`. Tests: `ExecutionManagerTests.Cancel_DoesNothing_WhenIdIsNullOrEmpty`, `Cancel_ReturnsFalse_WhenNothingMatches`, `Cancel_Type_DoesNothing_WhenNoMatchingJob`
  - **AC-002.3**: **Given** the exact Hangfire ID of an active job, including a Scheduled one, **When** `Cancel` is called, **Then** it is deleted without scanning and `true` is returned. Test: `ExecutionManagerTests.Cancel_DeletesExactHangfireId_WithoutScanning_EvenWhenScheduled`
  - **AC-002.4**: **Given** a Scheduled job that matches by custom ID, **When** `Cancel` is called, **Then** it is deleted. Test: `ExecutionManagerTests.Cancel_FindsScheduledJob_ByCustomId`
  - **AC-002.5**: **Given** a recurring job that is Scheduled (waiting for a retry), **When** `Cancel(Type)` is called, **Then** it is deleted. Test: `ExecutionManagerTests.Cancel_Type_FindsScheduledRecurringJob`

- **FR-003 Retry**: `Retry(string)` requeues the first Failed job matching the ID. `Retry(Type)` does the same for a recurring handler. Both return whether a job was requeued. *(Story 3)*
  - **AC-003.1**: **Given** Failed jobs, **When** `Retry` is called by ID or by handler type, **Then** the first match is requeued and `true` is returned, or `false` when nothing matches. Tests: `ExecutionManagerTests.Retry_RequeuesFailedJob_ReturnsTrue`, `Retry_ReturnsFalse_WhenNoFailedJobWithCustomId`, `Retry_ReturnsFalse_WhenCustomIdDoesNotMatch`, `Retry_Type_RequeuesFailedRecurringJob_ReturnsTrue`, `Retry_Type_ReturnsFalse_WhenNoFailedRecurringJob`, `Retry_Type_ReturnsFalse_WhenDifferentHandlerType`, `Retry_Type_ReturnsFalse_WhenFailedJobIsEventNotRecurring`

- **FR-004 List jobs by state**: `GetJobs(JobState)` lazily enumerates the ExecutionFlow jobs in a state. `JobState` is Enqueued, Processing, Succeeded, Failed, Cancelled (Hangfire's Deleted) and Scheduled. *(Story 4)*
  - **AC-004.1**: **Given** jobs in a state, **When** `GetJobs(state)` is enumerated, **Then** they are returned with the right `JobInfo`. Tests: `ExecutionManagerTests.GetJobs_Processing_ReturnsJobsWithCorrectJobInfo`, `GetJobs_Enqueued_QueriesEnqueuedJobs`, `GetJobs_Failed_QueriesFailedJobs`, `GetJobs_Cancelled_QueriesDeletedJobs`, `GetJobs_Succeeded_QueriesSucceededJobs`, `GetJobs_Scheduled_ListsScheduledJobs`, `GetJobs_ReturnsEmpty_WhenNoJobsExist`, `GetJobs_CustomIdIsNull_WhenNoCustomIdParameter`, `GetJobs_StateChangedAt_PopulatedCorrectly`
  - **AC-004.2**: **Given** a caller that consumes only part of the sequence, **When** it enumerates, **Then** only the storage pages needed are read, and nothing is read before enumeration starts. Test: `ExecutionManagerTests.GetJobs_IsLazy_AndReadsOnlyThePagesConsumed`
  - **AC-004.3**: **Given** a state that also holds other jobs, **When** `GetJobs` is enumerated, **Then** only ExecutionFlow jobs are returned: loaded jobs running `HangfireJobDispatcher`, and unloadable jobs whose invocation data names `HangfireJobDispatcher`. Tests: `ExecutionManagerTests.GetJobs_ExcludesNonExecutionFlowJobs_LoadedOrNot`, `GetJobs_EventTypeIsNull_WhenJobIsNull`
  - **AC-004.4**: **Given** a recurring job, **When** it is listed, **Then** `HandlerType` is set and `EventType`/`EventTypeName` are null; for unloadable jobs, all three are null. Tests: `ExecutionManagerTests.GetJobs_Recurring_HasHandlerType_AndNoEventType`, `GetJobs_EventTypeIsNull_WhenJobIsNull`
  - **AC-004.5**: **Given** a timestamp with `Kind = Unspecified`, **When** the job is listed, **Then** the timestamp is treated as UTC. Test: `ExecutionManagerTests.GetJobs_TreatsUnspecifiedTimestampsAsUtc` (it only catches a regression on a host outside UTC)

- **FR-005 Count jobs**: `CountJobs(JobState)` and `GetStateSummary()` read Hangfire's storage-wide statistics, including Scheduled. *(Story 4)*
  - **AC-005.1**: **Given** jobs in the storage, **When** `CountJobs`/`GetStateSummary` is called, **Then** the counts per state are returned, including Scheduled. Tests: `ExecutionManagerTests.CountJobs_ReturnsCorrectCount`, `CountJobs_ReturnsZero_WhenNoJobs`, `GetStateSummary_ReturnsAllCounts`, `GetStateSummary_ReturnsZeros_WhenNoJobs`, `CountJobs_And_Summary_IncludeScheduled`
  - **AC-005.2**: **Given** non-ExecutionFlow jobs in the storage, **When** jobs are counted, **Then** they are included, so counts can exceed what `GetJobs` returns. Documented (no test).

### Business Rules

- **RN-001** [CONFIRMED]: Matching by ID checks the Hangfire ID first, then the custom ID.
- **RN-002** [CONFIRMED]: With several jobs sharing a custom ID, `Cancel`/`Retry` act on the first match.
- **RN-003** [CONFIRMED]: `GetJobs` is lazy: enumerating twice queries twice, the connection stays open during enumeration, and the result isn't a snapshot. Documented in the XML docs and README.
- **RN-004** [CONFIRMED]: `IExecutionManager` is registered by full `Build()` and by `AddHangfireToExecutionFlow`.

### Key Entities

- **IExecutionManager**: query, cancel, retry, list and count jobs.
- **JobInfo**: one listed job (IDs, `EventType`/`EventTypeName`, `HandlerType`, `StateChangedAt`).
- **JobState**: Enqueued, Processing, Succeeded, Failed, Cancelled, Scheduled.
- **JobStateSummary**: counts per state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001): `GetJobs` memory use is bounded by the internal page size (10) plus what the caller keeps.
- **SC-002** (was NFR-002): Cancelling by exact Hangfire ID or by reservation key costs O(1) storage reads. Otherwise it's O(active jobs).

## Assumptions

- Out of scope: Awaiting jobs in `JobState` (continuations aren't exposed); changing a job's schedule.
- Breaking and behavior changes in 1.2.0:
  - `IExecutionManager.Cancel(string)` and `Cancel(Type)` return `bool`. This breaks implementers of the interface.
  - `JobState.Scheduled` was appended, so `switch` statements over `JobState` gain a case.
  - `GetJobs` is lazy and excludes non-ExecutionFlow jobs.
  - For recurring jobs, `JobInfo.EventTypeName` is null (it used to be `"DispatchRecurringAsync"`), and `HandlerType` is set.
- Open questions: none.
