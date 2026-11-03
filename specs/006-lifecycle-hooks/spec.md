# Feature Specification: Lifecycle hooks

**Feature Branch**: N/A (migrated)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the as-is mapping (see the findings in [research.md](research.md)). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/lifecycle-hooks/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` and `NFR` numbers kept).

**Packages**: `ExecutionFlow` (hook interfaces, event types, `HookErrorContext`), `ExecutionFlow.Hangfire` (`HangfireStateFilter`, `HookErrorHandler`)

**Amends**: none

## Context

Applications want to react to what happens to background jobs (alerting on failures, metrics, audit) without adding that code to every handler. Lifecycle hooks are classes that implement one or more `IOn*` interfaces. ExecutionFlow calls them when a job changes state.

Related features:
- [004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md): the `CustomId` passed to hooks.
- [005-recurring-jobs](../005-recurring-jobs/spec.md) and [003-dispatching](../003-dispatching/spec.md): the jobs whose states are observed.
- [002-setup-and-configuration](../002-setup-and-configuration/spec.md): `AddStateHandler`, `Build`.

**Actors**:

| Actor | Description |
| ----- | ----------- |
| Hook author | Implements `IOnEnqueued`, `IOnProcessing`, `IOnSucceeded`, `IOnFailed`, `IOnRetrying` and/or `IOnCancelled`. |
| Consumer app | Registers hooks with `options.AddStateHandler<T>()` and calls `Build()`. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Be notified when a job fails for good (Priority: P1)

As a hook author, I want to be notified when a job fails for good, so that I can alert someone.

**Why this priority**: alerting on final failures is the main use of hooks, and was broken (F-002).

**Independent Test**: register an `IOnFailed` hook, fail a job with retries through the real `AutomaticRetryAttribute`, and check the hook fires only on the final failure.

**Acceptance Scenarios**: AC-001.1, AC-001.2, AC-002.0..AC-002.9.

---

### User Story 2 - Measure attempt duration (Priority: P2)

As a hook author, I want to know how long an attempt took, so that I can record metrics.

**Why this priority**: metrics; was wrong outside UTC (F-001).

**Independent Test**: store a UTC `StartedAt`, elect Succeeded, and check `Duration`.

**Acceptance Scenarios**: AC-002.3, AC-003.1..AC-003.3.

---

### User Story 3 - Observe retries (Priority: P2)

As a hook author, I want to observe retries and their errors, so that I can see flaky jobs.

**Why this priority**: observability of flaky jobs; complements Story 1.

**Independent Test**: fail an attempt that will be retried and check `IOnRetrying` receives the exception once.

**Acceptance Scenarios**: AC-002.4, AC-002.8, AC-002.9, AC-003.3.

---

### User Story 4 - Hook bugs never affect jobs (Priority: P1)

As a consumer app, I want a bug in a hook to never affect the jobs themselves.

**Why this priority**: a throwing hook could break Hangfire's state election (F-003).

**Independent Test**: register a throwing hook and check other hooks run, the election is unchanged, and `HookErrorHandler` receives the error.

**Acceptance Scenarios**: AC-004.1..AC-004.3.

### Edge Cases

- A hook type that the service provider can't resolve: skipped. If resolving it throws, that's treated as a hook error (FR-004).
- A hook that implements several interfaces: called once per matching state.
- Reading the custom ID, retry count or duration fails: a warning is logged, and a default is used (null, attempt 1, or zero).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Register hooks**: `HangfireOptions.AddStateHandler<T>()` / `AddStateHandler(Type)` registers a hook type. On each state change, every registered type implementing the matching interface is resolved from the service provider and called. *(Story 1)*
  - **AC-001.1**: **Given** a registered hook type, **When** an event fires, **Then** it is resolved from the service provider; a type that can't be resolved (null) is skipped. Tests: `HangfireOptionsTests.AddStateHandler_Generic_AddsType`, `HangfireOptionsTests.AddStateHandler_Type_AddsType`, `HangfireValidationTests.StateFilter_CallsHandler_WhenServiceProviderReturnsInstance`
  - **AC-001.2**: **Given** null, **When** `AddStateHandler` is called, **Then** it throws. Test: `HangfireValidationTests.AddStateHandler_Throws_ForNullType`

- **FR-002 Hook per state**: `HangfireStateFilter` is registered with order 100 (`FilterOrder`), after Hangfire's `AutomaticRetryAttribute` (order 20), so it sees the final state decision. A retry appears as a candidate `ScheduledState` with the failed state in `ElectStateContext.TraversedStates`. *(Stories 1, 3)*
  - **AC-002.0**: **Given** a full setup, **When** `Build()` runs, **Then** the state filter is registered with an order greater than Hangfire's retry filter. Test: `HangfireSetupGlobalFiltersTests.Build_RegistersStateFilter_AfterHangfireRetryFilter`
  - **AC-002.1**: **Given** a job that isn't a retry, **When** it is enqueued, **Then** `IOnEnqueued` is called. Tests: `StateFilterTests.EnqueuedState_Calls_OnEnqueued`, `StateFilterTests.CustomId_Passed_InEvent`
  - **AC-002.2**: **Given** a job, **When** it starts processing, **Then** `IOnProcessing` is called. Test: `StateFilterTests.ProcessingState_Calls_OnProcessing`
  - **AC-002.3**: **Given** a job, **When** it succeeds, **Then** `IOnSucceeded` is called with the attempt's duration. Test: `StateFilterTests.SucceededState_Calls_OnSucceeded_WithDuration`
  - **AC-002.4**: **Given** a failed attempt that will be retried, **When** the state is elected, **Then** `IOnRetrying` is called once, with the attempt's exception, and `IOnFailed` is not called. Test: `StateFilterTests.FailedAttempt_ThatWillBeRetried_Calls_OnRetrying_WithException_NotOnFailed`
  - **AC-002.5**: **Given** a job with no retries left, **When** it fails, **Then** `IOnFailed` is called with the exception. Tests: `StateFilterTests.FailedState_Calls_OnFailed_WithException`, `StateFilterTests.FailedAttempt_WithNoRetriesLeft_Calls_OnFailed_NotOnRetrying`
  - **AC-002.6**: **Given** a retry policy that deletes a job after its last attempt (`OnAttemptsExceeded = Delete`), **When** the last attempt fails, **Then** `IOnFailed` is called, not `IOnCancelled`. Test: `StateFilterTests.RetriesExhausted_WithDeletePolicy_Calls_OnFailed_NotOnCancelled`
  - **AC-002.7**: **Given** a job, **When** it is deleted otherwise, **Then** `IOnCancelled` is called. Test: `StateFilterTests.DeletedState_Calls_OnCancelled`
  - **AC-002.8**: **Given** a failed job, **When** it is requeued manually, **Then** `IOnRetrying` (with no exception) is called, not `IOnEnqueued`. Test: `StateFilterTests.EnqueuedState_FromFailed_Calls_OnRetrying_NotOnEnqueued`
  - **AC-002.9**: **Given** a scheduled retry, **When** it becomes due, **Then** no hook is called, because `IOnRetrying` already fired. Test: `StateFilterTests.ScheduledRetryBecomingDue_CallsNoHook`

- **FR-003 Event data**: every event carries `JobId` (the Hangfire ID), `CustomId` and `HandlerType` (null when the handler isn't registered on this host). `Duration` exists only on `ExecutionSucceededEvent`, `ExecutionFailedEvent` and `ExecutionRetryingEvent`. `ExecutionFailedEvent` and `ExecutionRetryingEvent` carry `Exception`, and `ExecutionRetryingEvent` carries `AttemptNumber`. *(Stories 2, 3)*
  - **AC-003.1**: **Given** a job with a custom ID, **When** a hook fires, **Then** the job's custom ID is passed. Test: `StateFilterTests.CustomId_Passed_InEvent`
  - **AC-003.2**: **Given** a Processing state's `StartedAt` stored in UTC, **When** `Duration` is computed, **Then** it is `DateTime.UtcNow` minus `StartedAt` parsed as UTC, correct on any host time zone. Test: `StateFilterTests.Duration_IsMeasuredInUtc_OnAnyHostTimeZone` (it only catches a regression on a host outside UTC)
  - **AC-003.3**: **Given** the event types, **When** they are inspected, **Then** `ExecutionEvent` has no `Duration`, and the Succeeded/Failed/Retrying events store it (Retrying also stores the exception). Tests: `ExecutionEventTests.ExecutionEvent_HasNoDuration`, `ExecutionEventTests.ExecutionSucceededEvent_StoresDuration`, `ExecutionEventTests.ExecutionFailedEvent_StoresDuration`, `ExecutionEventTests.ExecutionRetryingEvent_StoresDuration`, `ExecutionEventTests.ExecutionRetryingEvent_StoresException`

- **FR-004 Hook isolation**: each hook is resolved and invoked in its own try/catch. A failure is sent to `HangfireOptions.HookErrorHandler` (`Action<HookErrorContext>`) when set, otherwise it's written with `Trace.TraceWarning`. An exception from `HookErrorHandler` itself is swallowed and traced. *(Story 4)*
  - **AC-004.1**: **Given** a hook that throws, **When** the state changes, **Then** the other hooks are still called and the job's state election is unchanged. Test: `StateFilterTests.ThrowingHook_DoesNotStopOtherHooks_NorTheElection`
  - **AC-004.2**: **Given** a hook that throws and `HookErrorHandler` set, **When** the state changes, **Then** the handler receives the hook type, hook interface, event and exception. Test: `StateFilterTests.ThrowingHook_IsReportedTo_HookErrorHandler`
  - **AC-004.3**: **Given** a `HookErrorHandler` that itself throws, **When** a hook fails, **Then** the error is swallowed. Test: `StateFilterTests.ThrowingHookErrorHandler_IsSwallowed`

### Business Rules

- **RN-001** [CONFIRMED]: Hooks run synchronously inside Hangfire's state transition, in the process where it happens. `OnEnqueued` for a publish runs in the producer. Producer-only hosts fire no hooks (constitution IV, was P-005). The other hooks run on the processing server. Documented in the README.
- **RN-002** [CONFIRMED]: A hook never affects the job (FR-004). To veto or change a state, use a Hangfire filter.
- **RN-003** [CONFIRMED]: Jobs created with `Schedule(...)` fire no hook until they're due. `PublishResult` is the way to record a schedule. Awaiting (continuations) fires no hook, and continuations aren't exposed. `IOnScheduled` was considered and not added.
- **RN-004** [CONFIRMED]: Hooks fire on the candidate state after the retry decision (order 100). A custom filter with a higher order that changes the candidate state afterwards would make hooks fire on a state that isn't final.
- **RN-005**: Hooks should be fast and hand heavy work to `Publish`. Documented in the README.

### Key Entities

- **`IOn*` interfaces**: `IOnEnqueued`, `IOnProcessing`, `IOnSucceeded`, `IOnFailed`, `IOnRetrying`, `IOnCancelled`.
- **ExecutionEvent** and subtypes: event data passed to hooks (FR-003).
- **HookErrorContext**: hook type, hook interface, event and exception of a failed hook.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001): A hook failure costs the job nothing but a log entry.

## Assumptions

- Out of scope: hooks for Awaiting (continuations aren't exposed), and `IOnScheduled`; vetoing or changing a state from a hook; async hooks.
- Open questions: none.

### Breaking and behavior changes in 1.2.0

- `ExecutionEvent.Duration` and the `duration` parameter of the `ExecutionEvent` constructor are removed. `Duration` now lives on `ExecutionSucceededEvent`, `ExecutionFailedEvent` and `ExecutionRetryingEvent`. The `ExecutionRetryingEvent` constructor gained an `exception` parameter before `duration`.
- `OnFailed` fires only on the final failure. Intermediate failures fire `OnRetrying`, now with the exception.
- A retry fires `OnRetrying` once, when it's scheduled, instead of again when it becomes due.
- Hook exceptions no longer propagate into Hangfire.
- `Duration` is now correct on hosts outside UTC (before, it was off by the UTC offset).
