# Spec: Lifecycle hooks

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (hook interfaces, event types, `HookErrorContext`), `ExecutionFlow.Hangfire` (`HangfireStateFilter`, `HookErrorHandler`)

## Context
Applications want to react to what happens to background jobs (alerting on failures, metrics, audit) without adding that code to every handler. Lifecycle hooks are classes that implement one or more `IOn*` interfaces. ExecutionFlow calls them when a job changes state.

Related capabilities:
- [custom-id-and-deduplication](../custom-id-and-deduplication/spec.md): the `CustomId` passed to hooks.
- [recurring-jobs](../recurring-jobs/spec.md) and [dispatching](../dispatching/spec.md): the jobs whose states are observed.
- [setup-and-configuration](../setup-and-configuration/spec.md): `AddStateHandler`, `Build`.

## Actors
| Actor | Description |
| ----- | ----------- |
| Hook author | Implements `IOnEnqueued`, `IOnProcessing`, `IOnSucceeded`, `IOnFailed`, `IOnRetrying` and/or `IOnCancelled`. |
| Consumer app | Registers hooks with `options.AddStateHandler<T>()` and calls `Build()`. |

## User stories
- US-001: As a hook author, I want to be notified when a job fails for good, so that I can alert someone.
- US-002: As a hook author, I want to know how long an attempt took, so that I can record metrics.
- US-003: As a hook author, I want to observe retries and their errors, so that I can see flaky jobs.
- US-004: As a consumer app, I want a bug in a hook to never affect the jobs themselves.

## Requirements

### REQ-001: Register hooks
`HangfireOptions.AddStateHandler<T>()` / `AddStateHandler(Type)` registers a hook type. On each state change, every registered type implementing the matching interface is resolved from the service provider and called.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN a hook type is registered, THE SYSTEM SHALL resolve it from the service provider for each event. A type that can't be resolved (null) is skipped. Tests: `HangfireOptionsTests.AddStateHandler_Generic_AddsType`, `AddStateHandler_Type_AddsType`, `HangfireValidationTests.StateFilter_CallsHandler_WhenServiceProviderReturnsInstance`
- AC-001.2: WHEN `AddStateHandler` is given null, THE SYSTEM SHALL throw. Test: `HangfireValidationTests.AddStateHandler_Throws_ForNullType`

### REQ-002: Hook per state
`HangfireStateFilter` is registered with order 100 (`FilterOrder`), after Hangfire's `AutomaticRetryAttribute` (order 20), so it sees the final state decision. A retry appears as a candidate `ScheduledState` with the failed state in `ElectStateContext.TraversedStates`.
Source: US-001, US-003

Acceptance criteria:
- AC-002.0: `Build()` SHALL register the state filter with an order greater than Hangfire's retry filter. Test: `HangfireSetupGlobalFiltersTests.Build_RegistersStateFilter_AfterHangfireRetryFilter`
- AC-002.1: WHEN a job is enqueued and it isn't a retry, THE SYSTEM SHALL call `IOnEnqueued`. Tests: `StateFilterTests.EnqueuedState_Calls_OnEnqueued`, `CustomId_Passed_InEvent`
- AC-002.2: WHEN a job starts processing, THE SYSTEM SHALL call `IOnProcessing`. Test: `StateFilterTests.ProcessingState_Calls_OnProcessing`
- AC-002.3: WHEN a job succeeds, THE SYSTEM SHALL call `IOnSucceeded` with the attempt's duration. Test: `StateFilterTests.SucceededState_Calls_OnSucceeded_WithDuration`
- AC-002.4: WHEN a failed attempt will be retried, THE SYSTEM SHALL call `IOnRetrying` once, with the attempt's exception, and SHALL NOT call `IOnFailed`. Test: `StateFilterTests.FailedAttempt_ThatWillBeRetried_Calls_OnRetrying_WithException_NotOnFailed`
- AC-002.5: WHEN a job fails with no retries left, THE SYSTEM SHALL call `IOnFailed` with the exception. Tests: `StateFilterTests.FailedState_Calls_OnFailed_WithException`, `FailedAttempt_WithNoRetriesLeft_Calls_OnFailed_NotOnRetrying`
- AC-002.6: WHEN the retry policy deletes a job after its last attempt (`OnAttemptsExceeded = Delete`), THE SYSTEM SHALL call `IOnFailed`, not `IOnCancelled`. Test: `StateFilterTests.RetriesExhausted_WithDeletePolicy_Calls_OnFailed_NotOnCancelled`
- AC-002.7: WHEN a job is deleted otherwise, THE SYSTEM SHALL call `IOnCancelled`. Test: `StateFilterTests.DeletedState_Calls_OnCancelled`
- AC-002.8: WHEN a failed job is requeued manually, THE SYSTEM SHALL call `IOnRetrying` (with no exception), not `IOnEnqueued`. Test: `StateFilterTests.EnqueuedState_FromFailed_Calls_OnRetrying_NotOnEnqueued`
- AC-002.9: WHEN a scheduled retry becomes due, THE SYSTEM SHALL call no hook, because `IOnRetrying` already fired. Test: `StateFilterTests.ScheduledRetryBecomingDue_CallsNoHook`

### REQ-003: Event data
Every event carries `JobId` (the Hangfire ID), `CustomId` and `HandlerType` (null when the handler isn't registered on this host). `Duration` exists only on `ExecutionSucceededEvent`, `ExecutionFailedEvent` and `ExecutionRetryingEvent`. `ExecutionFailedEvent` and `ExecutionRetryingEvent` carry `Exception`, and `ExecutionRetryingEvent` carries `AttemptNumber`.
Source: US-002, US-003

Acceptance criteria:
- AC-003.1: THE SYSTEM SHALL pass the job's custom ID. Test: `StateFilterTests.CustomId_Passed_InEvent`
- AC-003.2: `Duration` SHALL be `DateTime.UtcNow` minus the Processing state's `StartedAt`, with `StartedAt` parsed as UTC, and it SHALL be correct on any host time zone. Test: `StateFilterTests.Duration_IsMeasuredInUtc_OnAnyHostTimeZone` (it only catches a regression on a host outside UTC)
- AC-003.3: `ExecutionEvent` SHALL NOT have `Duration`. Tests: `ExecutionEventTests.ExecutionEvent_HasNoDuration`, `ExecutionSucceededEvent_StoresDuration`, `ExecutionFailedEvent_StoresDuration`, `ExecutionRetryingEvent_StoresDuration`, `ExecutionRetryingEvent_StoresException`

### REQ-004: Hook isolation
Each hook is resolved and invoked in its own try/catch. A failure is sent to `HangfireOptions.HookErrorHandler` (`Action<HookErrorContext>`) when set, otherwise it's written with `Trace.TraceWarning`. An exception from `HookErrorHandler` itself is swallowed and traced.
Source: US-004

Acceptance criteria:
- AC-004.1: WHEN a hook throws, THE SYSTEM SHALL still call the other hooks and SHALL NOT change the job's state election. Test: `StateFilterTests.ThrowingHook_DoesNotStopOtherHooks_NorTheElection`
- AC-004.2: WHEN a hook throws and `HookErrorHandler` is set, THE SYSTEM SHALL pass it the hook type, hook interface, event and exception. Test: `StateFilterTests.ThrowingHook_IsReportedTo_HookErrorHandler`
- AC-004.3: WHEN `HookErrorHandler` itself throws, THE SYSTEM SHALL swallow the error. Test: `StateFilterTests.ThrowingHookErrorHandler_IsSwallowed`

## Business rules
- RN-001 [CONFIRMED] Hooks run synchronously inside Hangfire's state transition, in the process where it happens. `OnEnqueued` for a publish runs in the producer. Producer-only hosts fire no hooks (P-005). The other hooks run on the processing server. Documented in the README.
- RN-002 [CONFIRMED] A hook never affects the job (REQ-004). To veto or change a state, use a Hangfire filter.
- RN-003 [CONFIRMED] Jobs created with `Schedule(...)` fire no hook until they're due. `PublishResult` is the way to record a schedule. Awaiting (continuations) fires no hook, and continuations aren't exposed. `IOnScheduled` was considered and not added.
- RN-004 [CONFIRMED] Hooks fire on the candidate state after the retry decision (order 100). A custom filter with a higher order that changes the candidate state afterwards would make hooks fire on a state that isn't final.
- RN-005 Hooks should be fast and hand heavy work to `Publish`. Documented in the README.

## Edge cases and errors
- A hook type that the service provider can't resolve: skipped. If resolving it throws, that's treated as a hook error (REQ-004).
- A hook that implements several interfaces: called once per matching state.
- Reading the custom ID, retry count or duration fails: a warning is logged, and a default is used (null, attempt 1, or zero).

## Non-functional requirements
- NFR-001: A hook failure costs the job nothing but a log entry.

## Out of scope
- Hooks for Awaiting (continuations aren't exposed), and `IOnScheduled`.
- Vetoing or changing a state from a hook.
- Async hooks.

## Breaking and behavior changes in 1.2.0
- `ExecutionEvent.Duration` and the `duration` parameter of the `ExecutionEvent` constructor are removed. `Duration` now lives on `ExecutionSucceededEvent`, `ExecutionFailedEvent` and `ExecutionRetryingEvent`. The `ExecutionRetryingEvent` constructor gained an `exception` parameter before `duration`.
- `OnFailed` fires only on the final failure. Intermediate failures fire `OnRetrying`, now with the exception.
- A retry fires `OnRetrying` once, when it's scheduled, instead of again when it becomes due.
- Hook exceptions no longer propagate into Hangfire.
- `Duration` is now correct on hosts outside UTC (before, it was off by the UTC offset).

## Open questions
- None.
