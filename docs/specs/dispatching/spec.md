# Spec: Dispatching

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md))
Packages: `ExecutionFlow` (contracts), `ExecutionFlow.Hangfire` (implementation)

## Context
Application code needs to hand work off to a background process without knowing about Hangfire. It publishes an event, either to run as soon as possible or at a later time. ExecutionFlow persists the event as a Hangfire job, and a consumer process later runs the handler registered for that event type.

Related capabilities:
- [custom-id-and-deduplication](../custom-id-and-deduplication/spec.md): how `ICustomIdEvent` and `DeduplicationBehavior` affect dispatching.
- [recurring-jobs](../recurring-jobs/spec.md): `IRecurringTrigger.Trigger`, which is also exposed by `IHangfireDispatcher`.
- [job-naming-and-dashboard](../job-naming-and-dashboard/spec.md): how the custom name is displayed.

## Actors
| Actor | Description |
| ----- | ----------- |
| Producer app | Calls `IEventDispatcher` to publish or schedule events. It can run in full mode or producer-only mode. |
| Consumer app | Runs a Hangfire server that executes the event handlers. |
| Handler author | Implements `IHandler<TEvent>` for an event type. |

## User stories
- US-001: As a producer app, I want to publish an event for immediate background processing, so that the caller isn't blocked by the work.
- US-002: As a producer app, I want to schedule an event after a delay or at a specific time, so that work runs later.
- US-003: As a producer app, I want an identifier for each published job, so that I can track or control it later.
- US-004: As a consumer app, I want each event to run the handler registered for its type, so that producers don't need to know about handlers.

## Requirements

### REQ-001: Publish an event for immediate processing
`IEventDispatcher.Publish<TEvent>(TEvent)` persists the event as a background job that is ready to run immediately.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN `Publish` is called and deduplication doesn't skip the event, THE SYSTEM SHALL enqueue one Hangfire job and return `PublishResult { Enqueued = true }` with a non-null `JobId`. Test: `DispatcherTests.Publish_ReturnsHangfireJobId_WhenNoCustomId`
- AC-001.2: WHEN `Publish` is called on a dispatcher created by `BuildDispatcherOnly`, THE SYSTEM SHALL enqueue the job the same way as in full mode. Test: `HangfireSetupTests.BuildDispatcherOnly_CanPublish`
- AC-001.3: WHEN `Publish` is called with a null event, THE SYSTEM SHALL throw `ArgumentNullException` and create no job. Test: `DispatcherTests.Publish_ThrowsArgumentNullException_WhenEventIsNull`

### REQ-002: Schedule an event after a delay
`IEventDispatcher.Schedule<TEvent>(TEvent, TimeSpan)` persists the event as a job that becomes ready to run once the delay has passed.
Source: US-002

Acceptance criteria:
- AC-002.1: WHEN `Schedule(event, delay)` is called and deduplication doesn't skip the event, THE SYSTEM SHALL create a scheduled Hangfire job due at *now + delay* and return `Enqueued = true` with a non-null `JobId`. Test: `DispatcherTests.Schedule_TimeSpan_ReturnsHangfireJobId_WhenNoCustomId`
- AC-002.2: WHEN `Schedule(event, delay)` is called with a null event, THE SYSTEM SHALL throw `ArgumentNullException` and create no job. Test: `DispatcherTests.Schedule_TimeSpan_ThrowsArgumentNullException_WhenEventIsNull`

### REQ-003: Schedule an event at a specific time
`IEventDispatcher.Schedule<TEvent>(TEvent, DateTimeOffset)` persists the event as a job that becomes ready to run at `enqueueAt`.
Source: US-002

Acceptance criteria:
- AC-003.1: WHEN `Schedule(event, enqueueAt)` is called and deduplication doesn't skip the event, THE SYSTEM SHALL create a scheduled Hangfire job due at `enqueueAt` and return `Enqueued = true` with a non-null `JobId`. Test: `DispatcherTests.Schedule_DateTimeOffset_ReturnsHangfireJobId_WhenNoCustomId`
- AC-003.2: WHEN `Schedule(event, enqueueAt)` is called with a null event, THE SYSTEM SHALL throw `ArgumentNullException` and create no job. Test: `DispatcherTests.Schedule_DateTimeOffset_ThrowsArgumentNullException_WhenEventIsNull`

### REQ-004: Job identifier in the publish result
`PublishResult.JobId` identifies the job created by `Publish`/`Schedule`.
Source: US-003

Acceptance criteria:
- AC-004.1: Given an event that doesn't implement `ICustomIdEvent`, When it is published or scheduled, Then `JobId` is the Hangfire job ID. Tests: `DispatcherTests.Publish_ReturnsHangfireJobId_WhenNoCustomId`, `Schedule_TimeSpan_ReturnsHangfireJobId_WhenNoCustomId`, `Schedule_DateTimeOffset_ReturnsHangfireJobId_WhenNoCustomId`
- AC-004.2: Given an event that implements `ICustomIdEvent` with a non-empty `CustomId`, When it is published or scheduled, Then `JobId` equals `CustomId`. Tests: `DispatcherTests.Publish_ReturnsCustomId_WhenEventImplementsICustomIdEvent`, `Schedule_TimeSpan_ReturnsCustomId_WhenEventImplementsICustomIdEvent`, `Schedule_DateTimeOffset_ReturnsCustomId_WhenEventImplementsICustomIdEvent`
- AC-004.3: Given an event that implements `ICustomIdEvent` with a null or empty `CustomId`, When it is published or scheduled, Then the event is treated as having no custom ID: `JobId` is the Hangfire job ID, no `CustomId` job parameter is written, and deduplication is skipped. Tests: `DispatcherTests.Publish_TreatsEmptyCustomIdAsNone`, `DispatcherTests.Publish_ReplaceExisting_DoesNotCancelUnrelatedJob_WhenCustomIdIsEmpty`
- AC-004.4: WHEN deduplication skips an event, THE SYSTEM SHALL return `PublishResult { JobId = null, Enqueued = false }` and create no job. This applies to `Publish` and both `Schedule` overloads. Tests: `DispatcherTests.Publish_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_TimeSpan_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`, `Schedule_DateTimeOffset_SkipIfExists_ReturnsFalse_WhenJobAlreadyRunning`

### REQ-005: Custom name travels with the job
When an event implements `ICustomNameEvent`, its `CustomName` is stored with the job so that it can be displayed and read during execution.
Source: US-003

Acceptance criteria:
- AC-005.1: WHEN an event that implements `ICustomNameEvent` is published or scheduled, THE SYSTEM SHALL store `CustomName` in the job arguments. Test: `DispatcherTests.Publish_StoresCustomNameInJobArgs_WhenEventImplementsICustomNameEvent`. The read side is covered by `HangfireJobInfoTests.EventJobInfo_GetExpectedName_Returns_CustomName_WhenAvailable`.
- AC-005.2: WHEN the handler runs, THE SYSTEM SHALL expose the custom name as the read-only flow parameter `CustomName`, which is null when the event has none. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExposesCustomNameParameter`

### REQ-006: Execute the registered handler for the event type
On the consumer side, the job resolves the handler registered for the event type and calls `HandleAsync` with a `FlowContext<TEvent>` that wraps the event.
Source: US-004

Acceptance criteria:
- AC-006.1: WHEN a published job runs and a handler is registered for its event type, THE SYSTEM SHALL resolve the handler and call `HandleAsync` with the original event. Tests: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ExecutesHandler`, `WithDI_DispatchEventAsync_ExecutesHandler`
- AC-006.2: WHEN the handler has constructor dependencies and DI is used, THE SYSTEM SHALL resolve them from the container. Test: `HangfireJobDispatcherTests.WithDI_DispatchEventAsync_HandlerWithDependency_ResolvesCorrectly`
- AC-006.3: WHEN a job runs and no handler is registered for its event type, THE SYSTEM SHALL fail the job with `InvalidOperationException("No handler registered for event type '<FullName>'.")`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_Throws_WhenEventNotRegistered`
- AC-006.4: WHEN a handler is registered but can't be activated (the provider returns null), THE SYSTEM SHALL fail the job with `InvalidOperationException("Could not activate handler instance for type '<type>'.")`. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_Throws_WhenHandlerNotResolvable`
- AC-006.5: WHEN the event implements `ICustomIdEvent` with a non-empty `CustomId`, THE SYSTEM SHALL make the custom ID available on the `FlowContext` during execution. Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_SetsCustomId`

### REQ-007: Custom ID is persisted atomically with the job
For an event with a custom ID, the job and its `CustomId` parameter are created together. A job never exists without its custom ID, and a failed publish never leaves a job behind.
Source: US-003

Acceptance criteria:
- AC-007.1: WHEN an `ICustomIdEvent` with a non-empty `CustomId` is published or scheduled and the job client implements `IBackgroundJobClientV2`, THE SYSTEM SHALL create the job and its `CustomId` parameter in a single `Create` call. Test: `DispatcherTests.Publish_CreatesJobWithCustomIdParameterAtomically_WhenClientSupportsV2`
- AC-007.2: WHEN the job client doesn't implement `IBackgroundJobClientV2`, THE SYSTEM SHALL create the job and then write the `CustomId` parameter. Known limitation: this path isn't atomic. Test: `DispatcherTests.Publish_SetsCustomId_WhenEventImplementsICustomIdEvent`
- AC-007.3: WHEN a `CustomId` was stored by an earlier version, THE SYSTEM SHALL still find the job by that custom ID. Test: `JobParametersTests.ReadCustomId_ReadsLegacyRawValue`

### REQ-008: Jobs with no registered handler
A job whose event type has no handler on the executing host is a configuration error that retrying won't fix, unless several consumers with different handlers share one queue.
Source: US-004

Acceptance criteria:
- AC-008.1: WHEN an event job runs on a host with no handler registered for its event type AND `HangfireOptions.RetryUnregisteredEventJobs` is `false` (default), THE SYSTEM SHALL fail the job without automatic retries. Test: `HandlerJobFilterProviderTests.UnregisteredEventJob_GetsRetryDisabled_ByDefault`
- AC-008.2: WHEN `RetryUnregisteredEventJobs` is `true`, THE SYSTEM SHALL apply Hangfire's retry policy. Test: `HandlerJobFilterProviderTests.UnregisteredEventJob_KeepsDefaultRetries_WhenOptionEnabled`

## Business rules
- RN-001 [CONFIRMED] Handlers are routed by the **compile-time** generic argument `TEvent` (the static type of the expression passed to `Publish`/`Schedule`), not by the runtime type of the event. The registry lookup is by exact type, and base classes and interfaces aren't searched. (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireJobDispatcher.cs`, `DispatchEventAsync`; `HangfireDispatcher` captures `TEvent` in the job expression.) Test: `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_RoutesByCompileTimeType`

  ```csharp
  // Only IHandler<OrderCreated> is registered. OrderCreatedExpress : OrderCreated.
  dispatcher.Publish(new OrderCreated());          // runs the OrderCreated handler
  OrderCreated e = new OrderCreatedExpress();
  dispatcher.Publish(e);                           // runs the OrderCreated handler (TEvent = OrderCreated)
  dispatcher.Publish(new OrderCreatedExpress());   // fails at execution: no handler for OrderCreatedExpress
  object o = new OrderCreated();
  dispatcher.Publish(o);                           // fails at execution: no handler for System.Object
  ```
- RN-002 [CONFIRMED] The producer doesn't check that a handler exists for `TEvent` when publishing. This is required for producer-only hosts, which don't know the handlers. Missing handlers are only detected when the job runs (REQ-008).
- RN-003 [CONFIRMED] Deduplication is checked before any job is created, and it applies the same way to `Publish` and to both `Schedule` overloads.
- RN-004 [CONFIRMED] For `ICustomIdEvent` events with a non-empty `CustomId`, the custom ID is saved as the `CustomId` job parameter when the job is created (REQ-007), and saved again when the job starts running. A null or empty `CustomId` means "no custom ID" everywhere (AC-004.3).
- ~~RN-005~~ (deprecated: replaced by REQ-008, 2026-09-30)
- RN-006 [CONFIRMED] Retries, queues, and other filters can be customized with Hangfire `JobFilterAttribute`s on the **handler** class. They're applied when the handler is registered on the host that evaluates the filters. (`Src/ExecutionFlow.Hangfire/Infrastructure/HandlerJobFilterProvider.cs`)

## Edge cases and errors
- `event` is null: `ArgumentNullException` (AC-001.3, AC-002.2, AC-003.2).
- `delay` is negative or zero, or `enqueueAt` is in the past: this is passed straight to Hangfire, which [INFERRED] makes the job ready immediately. It isn't tested.
- The storage is unavailable during `Publish`: the Hangfire exception propagates to the caller. [INFERRED]
- Storage fails while creating a job with a custom ID: nothing is persisted and the exception propagates (REQ-007), except with non-V2 clients (AC-007.2).
- The event type isn't serializable by Hangfire's serializer: the Hangfire exception propagates when publishing. [INFERRED]

## Non-functional requirements
- NFR-001 [UNKNOWN] There's no stated requirement for publish latency or throughput.
- NFR-002 [CONFIRMED] `Publish`/`Schedule` are synchronous (they return `PublishResult`, not `Task`).

## Out of scope
- Deduplication strategies and custom ID semantics beyond AC-004.3 (`custom-id-and-deduplication`).
- Triggering recurring jobs (`recurring-jobs`).
- Job state queries, cancellation, and retry (`execution-manager`).
- Choosing a queue per publish call (only possible through filters on the handler class).
- Continuations and batches (Hangfire features that aren't exposed).
- Routing by runtime type (rejected, see DEC-001 in [plan.md](plan.md)).

## Open questions
- None. Decisions are recorded in [plan.md](plan.md).
