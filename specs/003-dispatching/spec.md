# Feature Specification: Dispatching

**Feature Branch**: N/A (migrated)

**Created**: migrated from the custom SDD layout

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the brownfield as-is mapping (see [research.md](research.md), Findings)

**Input**: Migrated from `docs/specs/dispatching/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept; `NFR-n` → `SC-n`).

**Packages**: `ExecutionFlow` (contracts), `ExecutionFlow.Hangfire` (implementation)

**Amends**: none.

## Context

Application code needs to hand work off to a background process without knowing about Hangfire. It publishes an event, either to run as soon as possible or at a later time. ExecutionFlow persists the event as a Hangfire job, and a consumer process later runs the handler registered for that event type.

Related capabilities:
- [004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md): how `ICustomIdEvent` and `DeduplicationBehavior` affect dispatching.
- [005-recurring-jobs](../005-recurring-jobs/spec.md): `IRecurringTrigger.Trigger`, which is also exposed by `IHangfireDispatcher`.
- [008-job-naming-and-dashboard](../008-job-naming-and-dashboard/spec.md): how the custom name is displayed.

**Actors**: the producer app (calls `IEventDispatcher` to publish or schedule events; it can run in full mode or producer-only mode), the consumer app (runs a Hangfire server that executes the event handlers), and the handler author (implements `IHandler<TEvent>` for an event type).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Publish an event for immediate processing (Priority: P1)

As a producer app, I want to publish an event for immediate background processing, so that the caller isn't blocked by the work.

**Why this priority**: it is the core use of the library.

**Independent Test**: publish an event and check that one Hangfire job is enqueued and a `JobId` is returned.

**Acceptance Scenarios**: AC-001.1..AC-001.3.

---

### User Story 2 - Schedule an event (Priority: P1)

As a producer app, I want to schedule an event after a delay or at a specific time, so that work runs later.

**Why this priority**: the other half of the dispatching contract.

**Independent Test**: schedule an event with a `TimeSpan` and with a `DateTimeOffset` and check the scheduled job.

**Acceptance Scenarios**: AC-002.1, AC-002.2, AC-003.1, AC-003.2.

---

### User Story 3 - Identify each published job (Priority: P2)

As a producer app, I want an identifier for each published job, so that I can track or control it later.

**Why this priority**: needed for tracking and control, but publishing works without it.

**Independent Test**: publish events with and without a custom ID and check `PublishResult.JobId` and the stored parameters.

**Acceptance Scenarios**: AC-004.1..AC-004.4, AC-005.1, AC-005.2, AC-007.1..AC-007.3.

---

### User Story 4 - Run the handler registered for the event type (Priority: P1)

As a consumer app, I want each event to run the handler registered for its type, so that producers don't need to know about handlers.

**Why this priority**: without it no published work is ever executed.

**Independent Test**: run a dispatched job with and without a registered handler and check the outcome.

**Acceptance Scenarios**: AC-006.1..AC-006.5, AC-008.1, AC-008.2.

### Edge Cases

- `event` is null: `ArgumentNullException` (AC-001.3, AC-002.2, AC-003.2).
- `delay` is negative or zero, or `enqueueAt` is in the past: this is passed straight to Hangfire, which [INFERRED] makes the job ready immediately. It isn't tested.
- The storage is unavailable during `Publish`: the Hangfire exception propagates to the caller. [INFERRED]
- Storage fails while creating a job with a custom ID: nothing is persisted and the exception propagates (FR-007), except with non-V2 clients (AC-007.2).
- The event type isn't serializable by Hangfire's serializer: the Hangfire exception propagates when publishing. [INFERRED]

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Publish an event for immediate processing**: `IEventDispatcher.Publish<TEvent>(TEvent)` persists the event as a background job that is ready to run immediately. *(Story 1)*
  - **AC-001.1**: **Given** an event that deduplication doesn't skip, **When** `Publish` is called, **Then** one Hangfire job is enqueued and `PublishResult { Enqueued = true }` is returned with a non-null `JobId`. Test: `DispatcherTests.Publish_ReturnsHangfireJobId_WhenNoCustomId`
  - **AC-001.2**: **Given** a dispatcher created by `BuildDispatcherOnly`, **When** `Publish` is called, **Then** the job is enqueued the same way as in full mode. Test: `HangfireSetupTests.BuildDispatcherOnly_CanPublish`
  - **AC-001.3**: **Given** a null event, **When** `Publish` is called, **Then** `ArgumentNullException` is thrown and no job is created. Test: `DispatcherTests.Publish_ThrowsArgumentNullException_WhenEventIsNull`

- **FR-002 Schedule an event after a delay**: `IEventDispatcher.Schedule<TEvent>(TEvent, TimeSpan)` persists the event as a job that becomes ready to run once the delay has passed. *(Story 2)*
  - **AC-002.1**: **Given** an event that deduplication doesn't skip, **When** `Schedule(event, delay)` is called, **Then** a scheduled Hangfire job due at *now + delay* is created and `Enqueued = true` is returned with a non-null `JobId`. Test: `DispatcherTests.Schedule_TimeSpan_ReturnsHangfireJobId_WhenNoCustomId`
  - **AC-002.2**: **Given** a null event, **When** `Schedule(event, delay)` is called, **Then** `ArgumentNullException` is thrown and no job is created. Test: `DispatcherTests.Schedule_TimeSpan_ThrowsArgumentNullException_WhenEventIsNull`

- **FR-003 Schedule an event at a specific time**: `IEventDispatcher.Schedule<TEvent>(TEvent, DateTimeOffset)` persists the event as a job that becomes ready to run at `enqueueAt`. *(Story 2)*
  - **AC-003.1**: **Given** an event that deduplication doesn't skip, **When** `Schedule(event, enqueueAt)` is called, **Then** a scheduled Hangfire job due at `enqueueAt` is created and `Enqueued = true` is returned with a non-null `JobId`. Test: `DispatcherTests.Schedule_DateTimeOffset_ReturnsHangfireJobId_WhenNoCustomId`
  - **AC-003.2**: **Given** a null event, **When** `Schedule(event, enqueueAt)` is called, **Then** `ArgumentNullException` is thrown and no job is created. Test: `DispatcherTests.Schedule_DateTimeOffset_ThrowsArgumentNullException_WhenEventIsNull`

- **FR-004 Job identifier in the publish result**: `PublishResult.JobId` identifies the job created by `Publish`/`Schedule`. *(Story 3)*
  - **AC-004.1**: **Given** an event that doesn't implement `ICustomIdEvent`, **When** it is published or scheduled, **Then** `JobId` is the Hangfire job ID. Tests: `DispatcherTests.Publish_ReturnsHangfireJobId_WhenNoCustomId`, `Schedule_TimeSpan_ReturnsHangfireJobId_WhenNoCustomId`, `Schedule_DateTimeOffset_ReturnsHangfireJobId_WhenNoCustomId`
  - **AC-004.2**: **Given** an event that implements `ICustomIdEvent` with a non-empty `CustomId`, **When** it is published or scheduled, **Then** `JobId` equals `CustomId`. Tests: `DispatcherTests.Publish_ReturnsCustomId_WhenEventImplementsICustomIdEvent`, `Schedule_TimeSpan_ReturnsCustomId_WhenEventImplementsICustomIdEvent`, `Schedule_DateTimeOffset_ReturnsCustomId_WhenEventImplementsICustomIdEvent`
  - **AC-004.3**: **Given** an event that implements `ICustomIdEvent` with a null or empty `CustomId`, **When** it is published or scheduled, **Then** the event is treated as having no custom ID: `JobId` is the Hangfire job ID, no `CustomId` job parameter is written, and deduplication is skipped. Tests: `DispatcherTests.Publish_TreatsEmptyCustomIdAsNone`, `DispatcherTests.Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty`
  - **AC-004.4**: **Given** an event that deduplication skips, **When** it is published or scheduled (`Publish` and both `Schedule` overloads), **Then** `PublishResult { JobId = null, Enqueued = false }` is returned and no job is created. Tests: `DispatcherTests.Publish_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_TimeSpan_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_DateTimeOffset_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`

- **FR-005 Custom name travels with the job**: when an event implements `ICustomNameEvent`, its `CustomName` is stored with the job so that it can be displayed and read during execution. *(Story 3)*
  - **AC-005.1**: **Given** an event that implements `ICustomNameEvent`, **When** it is published or scheduled, **Then** `CustomName` is stored in the job arguments. Test: `DispatcherTests.Publish_StoresCustomNameInJobArgs_WhenEventImplementsICustomNameEvent`. The read side is covered by `HangfireJobInfoTests.EventJobInfo_GetExpectedName_Returns_CustomName_WhenAvailable`.
  - **AC-005.2**: **Given** a dispatched job, **When** the handler runs, **Then** the custom name is exposed as the read-only flow parameter `CustomName`, which is null when the event has none. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExposesCustomNameParameter`

- **FR-006 Execute the registered handler for the event type**: on the consumer side, the job resolves the handler registered for the event type and calls `HandleAsync` with a `FlowContext<TEvent>` that wraps the event. *(Story 4)*
  - **AC-006.1**: **Given** a handler registered for the event type, **When** a published job runs, **Then** the handler is resolved and `HandleAsync` is called with the original event. Tests: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExecutesHandler`, `WithDI_DispatchEventAsync_ExecutesHandler`
  - **AC-006.2**: **Given** a handler with constructor dependencies and DI in use, **When** the job runs, **Then** the dependencies are resolved from the container. Test: `HangfireJobDispatcherTests.WithDI_DispatchEventAsync_HandlerWithDependency_ResolvesCorrectly`
  - **AC-006.3**: **Given** no handler registered for the event type, **When** a job runs, **Then** the job fails with `InvalidOperationException("No handler registered for event type '<FullName>'.")`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_Throws_WhenEventNotRegistered`
  - **AC-006.4**: **Given** a registered handler that can't be activated (the provider returns null), **When** the job runs, **Then** the job fails with `InvalidOperationException("Could not activate handler instance for type '<type>'.")`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_Throws_WhenHandlerNotResolvable`
  - **AC-006.5**: **Given** an event that implements `ICustomIdEvent` with a non-empty `CustomId`, **When** the job runs, **Then** the custom ID is available on the `FlowContext`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_SetsCustomId`

- **FR-007 Custom ID is persisted atomically with the job**: for an event with a custom ID, the job and its `CustomId` parameter are created together. A job never exists without its custom ID, and a failed publish never leaves a job behind. *(Story 3)*
  - **AC-007.1**: **Given** an `ICustomIdEvent` with a non-empty `CustomId` and a job client that implements `IBackgroundJobClientV2`, **When** it is published or scheduled, **Then** the job and its `CustomId` parameter are created in a single `Create` call. Test: `DispatcherTests.Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2`
  - **AC-007.2**: **Given** a job client that doesn't implement `IBackgroundJobClientV2`, **When** an event with a custom ID is published, **Then** the job is created and then the `CustomId` parameter is written. Known limitation: this path isn't atomic. Test: `DispatcherTests.Publish_SetsCustomId_WhenEventImplementsICustomIdEvent`
  - **AC-007.3**: **Given** a `CustomId` stored by an earlier version, **When** the job is looked up by that custom ID, **Then** it is still found. Test: `JobParametersTests.ReadCustomId_ReadsLegacyRawValue`

- **FR-008 Jobs with no registered handler**: a job whose event type has no handler on the executing host is a configuration error that retrying won't fix, unless several consumers with different handlers share one queue. *(Story 4)*
  - **AC-008.1**: **Given** a host with no handler registered for the event type and `HangfireOptions.RetryUnregisteredEventJobs` = `false` (default), **When** the event job runs, **Then** the job fails without automatic retries. Test: `HandlerJobFilterProviderTests.UnregisteredEventJob_GetsRetryDisabled_ByDefault`
  - **AC-008.2**: **Given** `RetryUnregisteredEventJobs` = `true`, **When** such a job fails, **Then** Hangfire's retry policy applies. Test: `HandlerJobFilterProviderTests.UnregisteredEventJob_KeepsDefaultRetries_WhenOptionEnabled`

### Business Rules

- **RN-001** [CONFIRMED] Handlers are routed by the **compile-time** generic argument `TEvent` (the static type of the expression passed to `Publish`/`Schedule`), not by the runtime type of the event. The registry lookup is by exact type, and base classes and interfaces aren't searched. (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireJobDispatcher.cs`, `DispatchEventAsync`; `HangfireDispatcher` captures `TEvent` in the job expression.) See [research.md](research.md) R-001. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_RoutesByCompileTimeType`

  ```csharp
  // Only IHandler<OrderCreated> is registered. OrderCreatedExpress : OrderCreated.
  dispatcher.Publish(new OrderCreated());          // runs the OrderCreated handler
  OrderCreated e = new OrderCreatedExpress();
  dispatcher.Publish(e);                           // runs the OrderCreated handler (TEvent = OrderCreated)
  dispatcher.Publish(new OrderCreatedExpress());   // fails at execution: no handler for OrderCreatedExpress
  object o = new OrderCreated();
  dispatcher.Publish(o);                           // fails at execution: no handler for System.Object
  ```
- **RN-002** [CONFIRMED] The producer doesn't check that a handler exists for `TEvent` when publishing. This is required for producer-only hosts, which don't know the handlers. Missing handlers are only detected when the job runs (FR-008).
- **RN-003** [CONFIRMED] Deduplication is checked before any job is created, and it applies the same way to `Publish` and to both `Schedule` overloads.
- **RN-004** [CONFIRMED] For `ICustomIdEvent` events with a non-empty `CustomId`, the custom ID is saved as the `CustomId` job parameter when the job is created (FR-007), and saved again when the job starts running. A null or empty `CustomId` means "no custom ID" everywhere (AC-004.3).
- ~~**RN-005**~~ (deprecated: replaced by FR-008, 2026-09-30)
- **RN-006** [CONFIRMED] Retries, queues, and other filters can be customized with Hangfire `JobFilterAttribute`s on the **handler** class. They're applied when the handler is registered on the host that evaluates the filters. (`Src/ExecutionFlow.Hangfire/Infrastructure/HandlerJobFilterProvider.cs`)

### Key Entities

- **IEventDispatcher / IHangfireDispatcher**: the producer-side API (`Publish`, `Schedule`).
- **PublishResult**: `JobId` and `Enqueued` returned by every dispatch.
- **ICustomIdEvent / ICustomNameEvent**: optional event contracts that carry a custom ID and a display name.
- **HangfireOptions.RetryUnregisteredEventJobs**: retry policy for jobs whose handler isn't registered.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001) [UNKNOWN]: there's no stated requirement for publish latency or throughput.
- **SC-002** (was NFR-002) [CONFIRMED]: `Publish`/`Schedule` are synchronous (they return `PublishResult`, not `Task`).

## Assumptions

- Out of scope: deduplication strategies and custom ID semantics beyond AC-004.3 ([004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md)); triggering recurring jobs ([005-recurring-jobs](../005-recurring-jobs/spec.md)); job state queries, cancellation, and retry ([007-execution-manager](../007-execution-manager/spec.md)); choosing a queue per publish call (only possible through filters on the handler class); continuations and batches (Hangfire features that aren't exposed); routing by runtime type (rejected, see [research.md](research.md) R-001).
- Open questions: none. Decisions are recorded in [research.md](research.md).
