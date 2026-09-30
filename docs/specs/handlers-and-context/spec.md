# Spec: Handlers and context

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`IHandler`, `IHandler<TEvent>`, `FlowContext`, `FlowContextBuilder`, `FlowParameters`), `ExecutionFlow.Hangfire` (context creation in `HangfireJobDispatcher`)

## Context
Handlers are the application code that runs in the background. They receive a context with the event (for event handlers), a logger, per-execution parameters and a cancellation token. The context should give handlers what they need without tying them to the processor (ADR-0007).

Related: [dispatching](../dispatching/spec.md) (routing, REQ-006), [custom-id-and-deduplication](../custom-id-and-deduplication/spec.md) (`CustomId`, obsolete `SetCustomId`), [logging-and-console](../logging-and-console/spec.md) (the logger).

## Actors
| Actor | Description |
| ----- | ----------- |
| Handler author | Implements `IHandler<TEvent>` or `IHandler`. |
| Logger author | Reads the parameters through the logger factory. |

## User stories
- US-001: As a handler author, I want the event and a logger in one context object.
- US-002: As a handler author, I want to know which job and which attempt I'm running, without depending on Hangfire.
- US-003: As a handler author, I want to add values for this execution (e.g. for logging) without breaking the infrastructure's values.
- US-004: As a handler author, I want to stop cooperatively when the job is cancelled or the server shuts down.

## Requirements

### REQ-001: Handler contracts
`IHandler<TEvent>.HandleAsync(FlowContext<TEvent>, CancellationToken)` for events, and `IHandler.HandleAsync(FlowContext, CancellationToken)` for recurring jobs. A class implements exactly one of them, for one event type, and each event type has exactly one handler. (`Src/ExecutionFlow/Abstractions/IHandler.cs`, `ExecutionFlowOptions.Add`)
Source: US-001

Acceptance criteria:
- AC-001.1: [CONFIRMED] Tests: `ValidationTests.Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`, `Add_Throws_WhenTypeImplementsMultipleGenericIHandlers`, `Add_Throws_WhenDuplicateEventHandler_ForSameEventType`, `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExecutesHandler`, `WithoutDI_DispatchRecurringAsync_ExecutesHandler`

### REQ-002: Context content
`FlowContext` exposes `Log`, `Parameters`, and the processor-neutral job facts `JobId` (the processor's job ID, or `null` outside a processor) and `AttemptNumber` (1 for the first run, 2 for the first retry, ...), set through `FlowContextBuilder.SetJob`. `FlowContext<TEvent>` adds `Event` and `CustomId`. Hangfire-specific access is `context.GetPerformContext()` (Hangfire package).
Source: US-001, US-002

Acceptance criteria:
- AC-002.1: [CONFIRMED] Tests: `ContextTests.ExecutionContext_Exposes_Log_Property`, `ExecutionContext_TEvent_Exposes_Event_Property`, `CustomId_Is_Null_By_Default`, `FlowContextBuilderTests.Build_NonGeneric_ReturnsFlowContext`, `Build_Generic_ReturnsFlowContextWithEvent`, `Parameters_ArePassedToContext`, `Build_Generic_CreatesLogger`
- AC-002.2: Job facts. Tests: `FlowContextBuilderTests.Build_HasNoJobId_AndFirstAttempt_ByDefault`, `SetJob_ExposesJobIdAndAttempt_OnBothContextKinds`, `SetJob_Throws_ForAttemptBelowOne`, `SetJob_Throws_AfterBuild`, `HangfireJobDispatcherTests.DispatchEventAsync_ExposesJobIdAndFirstAttempt`, `DispatchEventAsync_ExposesAttemptNumber_OnRetry`
- AC-002.3: Typed Hangfire access. Tests: `HangfireJobDispatcherTests.GetPerformContext_ReturnsHangfiresContext`, `GetPerformContext_ReturnsNull_OutsideHangfire`

### REQ-003: Per-execution parameters
`FlowParameters` is a case-insensitive dictionary for one execution. The infrastructure adds read-only keys: `"PerformContext"` (Hangfire's `PerformContext`) and `"CustomName"`. Handlers can add, change and remove their own keys, and the logger sees the same instance. Parameters are not persisted, not passed to retries, and not shared with other jobs.
Source: US-003

Acceptance criteria:
- AC-003.1: [CONFIRMED] Tests: `FlowParametersTests.Add_NewKey_Succeeds`, `Add_CanUpdateOwnKey`, `Add_CanRemoveOwnKey`, `ReadOnlyKey_CanBeRead`, `ReadOnlyKey_CannotBeModified`, `ReadOnlyKey_CannotBeRemoved`, `ReadOnlyKey_CannotBeAddedViaAdd`, `TryGetValue_ReturnsTrue_ForExistingKey`, `TryGetValue_ReturnsFalse_ForMissingKey`, `ContainsKey_ReturnsTrueForBothReadOnlyAndUserKeys`, `Count_IncludesBothReadOnlyAndUserKeys`, `Clear_RemovesOnlyUserKeys`, `Enumeration_IncludesAllKeys`, `MixedUsage_ReadOnlyAndUserKeys_WorkTogether`
- AC-003.2: Read-only keys are protected in any casing, like the dictionary itself. Tests: `FlowParametersTests.ReadOnlyKey_CannotBeModified_InAnyCasing`, `ReadOnlyKey_CannotBeRemoved_InAnyCasing`
- AC-003.3: Infrastructure keys are not public API: they stay internal (`ContextConsts`). Handlers use `context.GetPerformContext()` for Hangfire, and the event (`ICustomNameEvent`) for the custom name.

### REQ-004: Context builder
`FlowContextBuilder` builds one context and then refuses changes.
Source: US-001

Acceptance criteria:
- AC-004.1: [CONFIRMED] Tests: `FlowContextBuilderTests.Build_NonGeneric_Throws_WhenCalledTwice`, `Build_Generic_Throws_WhenCalledTwice`, `Build_Generic_Throws_AfterNonGenericBuild`, `Build_NonGeneric_Throws_AfterGenericBuild`, `Add_Throws_AfterBuild`, `AddReadOnly_Throws_AfterBuild`, `Build_Generic_InvokesOnCustomIdChange`, `FlowContextDisposeTests.Dispose_PreventsOnCustomIdChangeCallback`, `UsingStatement_DisposesContext`

### REQ-005: Cancellation
The `CancellationToken` is Hangfire's job token. It fires when the server shuts down, or when the job is deleted (e.g. `IExecutionManager.Cancel`, `ReplaceExisting`), within Hangfire's cancellation check interval. [INFERRED from Hangfire's behavior; not tested here]
Source: US-004

## Business rules
- RN-001 [CONFIRMED] One handler per event type (REQ-001). An event that needs several independent actions is split into several events, each with its own job, retries and name. Decided to keep (2026-09-30).
- RN-002 [CONFIRMED] Handlers are transient with DI. Without DI, they're transient unless registered as singletons in the built-in activator (setup-and-configuration REQ-004).

## Out of scope
- Several handlers per event (fan-out): rejected (RN-001).
- Persisting parameters across attempts.

## Breaking and behavior changes in 1.2.0
- Writing or removing a read-only parameter with a different casing (e.g. `"performcontext"`) now throws; it used to silently overwrite it.
- New: `FlowContext.JobId`, `FlowContext.AttemptNumber`, `FlowContextBuilder.SetJob`, `FlowContextExtensions.GetPerformContext()`.

## Open questions
- None.
