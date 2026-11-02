# Feature Specification: Handlers and context

**Feature Branch**: `main` (brownfield capability)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the as-is mapping (findings in [research.md](research.md))

**Input**: Migrated from `docs/specs/handlers-and-context/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept).

**Packages**: `ExecutionFlow` (`IHandler`, `IHandler<TEvent>`, `FlowContext`, `FlowContextBuilder`, `FlowParameters`), `ExecutionFlow.Hangfire` (context creation in `HangfireJobDispatcher`)

**Amends**: none

## Context

Handlers are the application code that runs in the background. They receive a context with the event (for event handlers), a logger, per-execution parameters and a cancellation token. The context should give handlers what they need without tying them to the processor ([constitution](../../.specify/memory/constitution.md) principle II).

Related: [003-dispatching](../003-dispatching/spec.md) (routing, FR-006), [004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md) (`CustomId`, obsolete `SetCustomId`), [009-logging-and-console](../009-logging-and-console/spec.md) (the logger).

**Actors**: the handler author (implements `IHandler<TEvent>` or `IHandler`) and the logger author (reads the parameters through the logger factory).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Event and logger in one context (Priority: P1)

As a handler author, I want the event and a logger in one context object.

**Why this priority**: it is the basic handler contract; nothing runs without it.

**Independent Test**: register a handler without DI, dispatch an event and a recurring job, and check the handler receives a context with `Event` and `Log`.

**Acceptance Scenarios**: AC-001.1, AC-002.1, AC-004.1.

---

### User Story 2 - Know the job and the attempt (Priority: P2)

As a handler author, I want to know which job and which attempt I'm running, without depending on Hangfire.

**Why this priority**: needed for logging, correlation and idempotency, but handlers run without it.

**Independent Test**: dispatch an event through `HangfireJobDispatcher`, on a first run and on a retry, and read `JobId` and `AttemptNumber`.

**Acceptance Scenarios**: AC-002.2, AC-002.3.

---

### User Story 3 - Per-execution parameters (Priority: P2)

As a handler author, I want to add values for this execution (e.g. for logging) without breaking the infrastructure's values.

**Why this priority**: mainly serves the logger; the infrastructure keys must stay safe.

**Independent Test**: add, change and remove user keys and try to overwrite read-only keys in any casing.

**Acceptance Scenarios**: AC-003.1, AC-003.2, AC-003.3.

---

### User Story 4 - Cooperative cancellation (Priority: P3)

As a handler author, I want to stop cooperatively when the job is cancelled or the server shuts down.

**Why this priority**: it relies on Hangfire's own token; ExecutionFlow only passes it through.

**Independent Test**: none here (FR-005 is inferred from Hangfire's behavior and not tested in this repo).

**Acceptance Scenarios**: none (FR-005 has no AC).

### Edge Cases

- A null `PerformContext` (outside Hangfire): `JobId` stays `null`, `AttemptNumber` stays 1, and `GetPerformContext()` returns `null`.
- Writing or removing a read-only parameter with a different casing (e.g. `"performcontext"`) throws.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Handler contracts**: `IHandler<TEvent>.HandleAsync(FlowContext<TEvent>, CancellationToken)` for events, and `IHandler.HandleAsync(FlowContext, CancellationToken)` for recurring jobs. A class implements exactly one of them, for one event type, and each event type has exactly one handler. (`Src/ExecutionFlow/Abstractions/IHandler.cs`, `ExecutionFlowOptions.Add`) *(Story 1)*
  - **AC-001.1** [CONFIRMED]: **Given** handler types, **When** they are registered and dispatched, **Then** a type implementing both contracts, several generic contracts, or a second handler for the same event type is rejected, and a valid event or recurring handler runs without DI. Tests: `ValidationTests.Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`, `ValidationTests.Add_Throws_WhenTypeImplementsMultipleGenericIHandlers`, `ValidationTests.Add_Throws_WhenDuplicateEventHandler_ForSameEventType`, `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExecutesHandler`, `HangfireJobDispatcherTests.WithoutDI_DispatchRecurringAsync_ExecutesHandler`

- **FR-002 Context content**: `FlowContext` exposes `Log`, `Parameters`, and the processor-neutral job facts `JobId` (the processor's job ID, or `null` outside a processor) and `AttemptNumber` (1 for the first run, 2 for the first retry, ...), set through `FlowContextBuilder.SetJob`. `FlowContext<TEvent>` adds `Event` and `CustomId`. Hangfire-specific access is `context.GetPerformContext()` (Hangfire package). *(Stories 1, 2)*
  - **AC-002.1** [CONFIRMED]: **Given** a context built by `FlowContextBuilder`, **When** it is read, **Then** it exposes `Log`, `Parameters`, and (generic) `Event`, with `CustomId` null by default. Tests: `ContextTests.ExecutionContext_Exposes_Log_Property`, `ContextTests.ExecutionContext_TEvent_Exposes_Event_Property`, `ContextTests.CustomId_Is_Null_By_Default`, `FlowContextBuilderTests.Build_NonGeneric_ReturnsFlowContext`, `FlowContextBuilderTests.Build_Generic_ReturnsFlowContextWithEvent`, `FlowContextBuilderTests.Parameters_ArePassedToContext`, `FlowContextBuilderTests.Build_Generic_CreatesLogger`
  - **AC-002.2**: **Given** a builder with or without `SetJob`, **When** the context is built (directly or by `HangfireJobDispatcher`, on a first run or a retry), **Then** it exposes the job facts (`JobId` null and attempt 1 by default; attempt below 1 or `SetJob` after `Build` throws). Tests: `FlowContextBuilderTests.Build_HasNoJobId_AndFirstAttempt_ByDefault`, `FlowContextBuilderTests.SetJob_ExposesJobIdAndAttempt_OnBothContextKinds`, `FlowContextBuilderTests.SetJob_Throws_ForAttemptBelowOne`, `FlowContextBuilderTests.SetJob_Throws_AfterBuild`, `HangfireJobDispatcherTests.DispatchEventAsync_ExposesJobIdAndFirstAttempt`, `HangfireJobDispatcherTests.DispatchEventAsync_ExposesAttemptNumber_OnRetry`
  - **AC-002.3**: **Given** a context, **When** `GetPerformContext()` is called, **Then** it returns Hangfire's `PerformContext` inside Hangfire and `null` outside. Tests: `HangfireJobDispatcherTests.GetPerformContext_ReturnsHangfiresContext`, `HangfireJobDispatcherTests.GetPerformContext_ReturnsNull_OutsideHangfire`

- **FR-003 Per-execution parameters**: `FlowParameters` is a case-insensitive dictionary for one execution. The infrastructure adds read-only keys: `"PerformContext"` (Hangfire's `PerformContext`) and `"CustomName"`. Handlers can add, change and remove their own keys, and the logger sees the same instance. Parameters are not persisted, not passed to retries, and not shared with other jobs. *(Story 3)*
  - **AC-003.1** [CONFIRMED]: **Given** a `FlowParameters` with read-only and user keys, **When** keys are added, updated, removed, read, counted, cleared or enumerated, **Then** user keys are editable, read-only keys can only be read, and `Clear` removes only user keys. Tests: `FlowParametersTests.Add_NewKey_Succeeds`, `FlowParametersTests.Add_CanUpdateOwnKey`, `FlowParametersTests.Add_CanRemoveOwnKey`, `FlowParametersTests.ReadOnlyKey_CanBeRead`, `FlowParametersTests.ReadOnlyKey_CannotBeModified`, `FlowParametersTests.ReadOnlyKey_CannotBeRemoved`, `FlowParametersTests.ReadOnlyKey_CannotBeAddedViaAdd`, `FlowParametersTests.TryGetValue_ReturnsTrue_ForExistingKey`, `FlowParametersTests.TryGetValue_ReturnsFalse_ForMissingKey`, `FlowParametersTests.ContainsKey_ReturnsTrueForBothReadOnlyAndUserKeys`, `FlowParametersTests.Count_IncludesBothReadOnlyAndUserKeys`, `FlowParametersTests.Clear_RemovesOnlyUserKeys`, `FlowParametersTests.Enumeration_IncludesAllKeys`, `FlowParametersTests.MixedUsage_ReadOnlyAndUserKeys_WorkTogether`
  - **AC-003.2**: **Given** a read-only key, **When** it is written or removed with a different casing, **Then** it is protected, like the dictionary itself is case-insensitive. Tests: `FlowParametersTests.ReadOnlyKey_CannotBeModified_InAnyCasing`, `FlowParametersTests.ReadOnlyKey_CannotBeRemoved_InAnyCasing`
  - **AC-003.3**: **Given** a handler that needs infrastructure values, **When** it accesses them, **Then** it uses `context.GetPerformContext()` for Hangfire and the event (`ICustomNameEvent`) for the custom name; the infrastructure keys are not public API and stay internal (`ContextConsts`). Test: none (API-shape decision; see [research.md](research.md) F-003)

- **FR-004 Context builder**: `FlowContextBuilder` builds one context and then refuses changes. *(Story 1)*
  - **AC-004.1** [CONFIRMED]: **Given** a builder that has built a context, **When** it is built again or modified, **Then** it throws; a disposed context no longer invokes the custom ID callback. Tests: `FlowContextBuilderTests.Build_NonGeneric_Throws_WhenCalledTwice`, `FlowContextBuilderTests.Build_Generic_Throws_WhenCalledTwice`, `FlowContextBuilderTests.Build_Generic_Throws_AfterNonGenericBuild`, `FlowContextBuilderTests.Build_NonGeneric_Throws_AfterGenericBuild`, `FlowContextBuilderTests.Add_Throws_AfterBuild`, `FlowContextBuilderTests.AddReadOnly_Throws_AfterBuild`, `FlowContextBuilderTests.Build_Generic_InvokesOnCustomIdChange`, `FlowContextDisposeTests.Dispose_PreventsOnCustomIdChangeCallback`, `FlowContextDisposeTests.UsingStatement_DisposesContext`

- **FR-005 Cancellation**: the `CancellationToken` is Hangfire's job token. It fires when the server shuts down, or when the job is deleted (e.g. `IExecutionManager.Cancel`, `ReplaceExisting`), within Hangfire's cancellation check interval. [INFERRED from Hangfire's behavior; not tested here] *(Story 4)*

### Business Rules

- **RN-001** [CONFIRMED]: One handler per event type (FR-001). An event that needs several independent actions is split into several events, each with its own job, retries and name. Decided to keep (2026-09-30).
- **RN-002** [CONFIRMED]: Handlers are transient with DI. Without DI, they're transient unless registered as singletons in the built-in activator ([002-setup-and-configuration](../002-setup-and-configuration/spec.md) FR-004).

### Key Entities

- **IHandler / IHandler&lt;TEvent&gt;**: the recurring and event handler contracts.
- **FlowContext / FlowContext&lt;TEvent&gt;**: what a handler receives (log, parameters, job facts, event, custom ID).
- **FlowContextBuilder**: builds one context, then refuses changes.
- **FlowParameters**: case-insensitive per-execution dictionary with read-only infrastructure keys.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- None (the old spec had no NFRs). Behavior is measured by the AC tests above.

## Assumptions

- Out of scope: several handlers per event (fan-out), rejected (RN-001); persisting parameters across attempts.
- Breaking and behavior changes in 1.2.0: writing or removing a read-only parameter with a different casing (e.g. `"performcontext"`) now throws; it used to silently overwrite it. New: `FlowContext.JobId`, `FlowContext.AttemptNumber`, `FlowContextBuilder.SetJob`, `FlowContextExtensions.GetPerformContext()`.
- Open questions: none.
