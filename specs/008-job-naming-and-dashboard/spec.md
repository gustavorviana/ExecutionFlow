# Feature Specification: Job naming and dashboard

**Feature Branch**: N/A (migrated)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the as-is mapping (see the findings in [research.md](research.md)). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/job-naming-and-dashboard/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept).

**Packages**: `ExecutionFlow` (`ICustomNameEvent`), `ExecutionFlow.Hangfire` (`IHangfireJobName`, `DefaultHangfireJobName`, `JobDisplayNameResolver`, `DashboardOptionsExtensions`, `HangfireJobInfo`)

**Amends**: none

**Decision record**: [research.md](research.md) R-002 (was ADR-0006). The chain below supersedes step 4 of that decision.

## Context

Every ExecutionFlow job runs through the same internal method (`HangfireJobDispatcher.DispatchEventAsync`/`DispatchRecurringAsync`), so Hangfire's dashboard would show the same meaningless name for all of them. ExecutionFlow resolves a meaningful name for its own jobs, and leaves other jobs with Hangfire's default names.

Related capabilities:
- [003-dispatching](../003-dispatching/spec.md): `ICustomNameEvent.CustomName` travels in the job arguments (FR-005 there).
- [005-recurring-jobs](../005-recurring-jobs/spec.md): recurring job IDs (`IJobIdGenerator`), which is also the last naming fallback.

**Actors**:
- **Event/handler author**: names jobs through `ICustomNameEvent`, `[DisplayName]` or `[JobDisplayName]`.
- **Dashboard operator**: reads job names in the Hangfire dashboard.
- **Consumer/dashboard host**: calls `UseExecutionFlowJobNames`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A meaningful name for each ExecutionFlow job (Priority: P1)

As a dashboard operator, I want each ExecutionFlow job to show a meaningful name.

**Why this priority**: it is the reason the feature exists; without it every job shows the dispatcher method.

**Independent Test**: resolve the name of event and recurring jobs with and without registered handlers.

**Acceptance Scenarios**: AC-001.1..AC-001.7, AC-004.1, AC-004.2.

---

### User Story 2 - Name a specific job instance from a core-only project (Priority: P2)

As an event author, I want to name a specific job instance (e.g. "Reminder for order 42"), from a project that only references the core.

**Why this priority**: refines names per instance; the type-based name already works without it.

**Independent Test**: publish an event implementing `ICustomNameEvent` and read the name back from the job.

**Acceptance Scenarios**: AC-001.2, AC-002.1, AC-002.2.

---

### User Story 3 - Leave other jobs alone (Priority: P1)

As a dashboard operator, I want jobs that aren't ExecutionFlow's to keep their `[JobDisplayName]`, and not be named as if they were ExecutionFlow events.

**Why this priority**: the as-is behavior broke names of unrelated jobs (F-002).

**Independent Test**: name native jobs (plain, generic, with `[JobDisplayName]`).

**Acceptance Scenarios**: AC-003.1.

---

### User Story 4 - `[JobDisplayName]` on handlers (Priority: P3)

As a handler author who knows Hangfire, I want `[JobDisplayName]` to work on my handler.

**Why this priority**: convenience for Hangfire users; other naming sources exist.

**Independent Test**: put `[JobDisplayName("... {0}")]` on `HandleAsync` and resolve the name.

**Acceptance Scenarios**: AC-001.5..AC-001.7.

### Edge Cases

- An empty `CustomName` is ignored (AC-001.2).
- `[JobDisplayName]` with unknown placeholders, or on a recurring job (no event): the text is used as written (AC-001.7).
- The dashboard has no `IHangfireJobName` registered: every job keeps its default name, with no exception and no warning (AC-004.2).
- A producer-only host has no handlers registered: names come from the carried type (RN-004).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Name resolution**: *(Stories 1, 2, 4)*

  Attribute placement: `[DisplayName]` goes on **classes** (the handler class, or the event class). Only Hangfire's `[JobDisplayName]` goes on a **method**: the handler's `HandleAsync`, or a native job's method.

  Two classes split the rules. Each step is a `protected` method of the base, so a derived class can place its own steps between them:
  - **`JobDisplayNameResolver`** (concrete, in `ExecutionFlow.Hangfire`) holds the ExecutionFlow steps, with nothing Hangfire-specific:
    - `GetConfiguredName(job)`: the `CustomName` stored in the job (event jobs), or `null`.
    - `GetHandlerDisplayName(job)`: `[DisplayName]` on the registered handler class, or `null`.
    - `GetCarriedTypeDisplayName(job)`: `[DisplayName]` on the type the job carries (the event class, or the recurring handler class), **only when the handler isn't registered on this host** (e.g. producer-only), or `null`.
    - `GetFallbackName(job)`: `IJobIdGenerator.GenerateId(type)` (see RN-006).
    - `public virtual string GetName(Job job)` = configured > handler `[DisplayName]` > carried type `[DisplayName]` > fallback.

    All steps return `null` for non-ExecutionFlow jobs, except the fallback.
  - **`DefaultHangfireJobName : JobDisplayNameResolver, IHangfireJobName`** overrides `GetName` to place Hangfire's `[JobDisplayName]` **before the handler class's `[DisplayName]`**. The attribute is read from the handler's `HandleAsync` for ExecutionFlow jobs, with `{0}` = the event, and from the job method for other jobs, formatted with the job's arguments.

  Full order in `DefaultHangfireJobName`: `CustomName` > `[JobDisplayName]` on `HandleAsync` > handler class `[DisplayName]` > event class `[DisplayName]` (handler not registered) > `GenerateId`.

  - **AC-001.1**: **Given** jobs with names defined at different levels, **When** `JobDisplayNameResolver` resolves them, **Then** the precedence above is followed. Tests: `JobDisplayNameResolverTests.GetName_UsesCustomName_First`, `GetName_UsesHandlerDisplayName_WhenNoCustomName`, `GetName_UsesEventDisplayName_WhenHandlerNotRegistered`, `GetName_UsesRecurringHandlerDisplayName_WhenNotRegistered`, `GetName_IgnoresEventDisplayName_WhenHandlerRegistered`, `DefaultHangfireJobNameTests.GetName_ReturnsDisplayName_ForRegisteredEventHandler`, `GetName_ReturnsDisplayName_ForRegisteredRecurringHandler`
  - **AC-001.2**: **Given** an empty `CustomName`, **When** the name is resolved, **Then** it is ignored. Tests: `JobDisplayNameResolverTests.GetName_IgnoresEmptyCustomName`, `HangfireJobInfoTests.EventJobInfo_CustomJobName_Null_WhenEmpty`, `EventJobInfo_CustomJobName_Null_WhenNotProvided`
  - **AC-001.3**: **Given** a handler with Hangfire's `[JobDisplayName]`, **When** `JobDisplayNameResolver` resolves the name, **Then** the attribute is not read. Test: `JobDisplayNameResolverTests.GetName_IgnoresHangfireJobDisplayName`
  - **AC-001.4**: **Given** no name defined anywhere, **When** `JobDisplayNameResolver.GetName` is called, **Then** it falls back to `GenerateId`. Tests: `JobDisplayNameResolverTests.GetName_FallsBackToIdGenerator_ForRegisteredHandler`, `GetName_FallsBackToIdGenerator_ForEventType_WhenHandlerNotRegistered`, `GetName_FallsBackToIdGenerator_ForRecurringHandler_WhenNotRegistered`, `GetName_NativeJob_FallsBackToIdGenerator_WithJobType`
  - **AC-001.5**: **Given** a handler whose `HandleAsync` has `[JobDisplayName("... {0}")]` and no `CustomName`, **When** `DefaultHangfireJobName` resolves the name, **Then** it uses the attribute with `{0}` = the event (`ToString()`). Test: `DefaultHangfireJobNameTests.GetName_UsesHandlerJobDisplayName_WithEventAsPlaceholder`
  - **AC-001.6**: **Given** names at several levels, **When** `DefaultHangfireJobName` resolves them, **Then** `CustomName` wins over `[JobDisplayName]`, and `[JobDisplayName]` wins over the handler class's `[DisplayName]`. Tests: `DefaultHangfireJobNameTests.GetName_CustomName_WinsOverJobDisplayName`, `GetName_JobDisplayName_WinsOverHandlerDisplayName`, `GetName_UsesHandlerDisplayName_WhenNoJobDisplayName`
  - **AC-001.7**: **Given** a `[JobDisplayName]` with placeholders other than `{0}`, or a recurring job (no event), **When** the name is resolved, **Then** the text is used as written. Test: `DefaultHangfireJobNameTests.GetName_JobDisplayNameWithUnknownPlaceholder_UsesTextAsIs`

- **FR-002 Per-instance name**: an event implementing `ICustomNameEvent` (in the core, namespace `ExecutionFlow.Abstractions`) names its own job. An events project needs only the core package. *(Story 2)*
  - **AC-002.1**: **Given** an event implementing `ICustomNameEvent`, **When** it is published, **Then** its `CustomName` is stored in the job arguments and read back as the job's custom name. Tests: `HangfireJobInfoTests.EventJobInfo_CustomJobName_Extracted_WhenProvided`, `DispatcherTests.Publish_StoresCustomNameInJobArgs_WhenEventImplementsICustomNameEvent`
  - **AC-002.2**: **Given** an events project that references only `ExecutionFlow`, **When** it uses `ICustomNameEvent`, **Then** it builds, because the interface lives in the core package. Verified by the build: `Examples/ExecutionFlow.Examples.Shared` uses it and references only `ExecutionFlow` (no test).

- **FR-003 Other jobs**: `HangfireJobInfo.Create` returns `null` for non-ExecutionFlow jobs. Their name is `[JobDisplayName]` on the job method, formatted with the job's arguments (the text as written if formatting fails), otherwise `IJobIdGenerator.GenerateId(job.Type)` (the job's type, e.g. the interface in `Enqueue<IService>(x => x.Run())`). When the dashboard has no `IHangfireJobName` (FR-004), Hangfire's own default (`Type.Method`) is used. *(Story 3)*
  - **AC-003.1**: **Given** non-ExecutionFlow jobs (plain, generic, with `[JobDisplayName]`), **When** their name is resolved, **Then** they are not treated as ExecutionFlow jobs and get the names above. Tests: `DefaultHangfireJobNameTests.GetName_NativeJob_FallsBackToIdGenerator`, `GetName_GenericNativeJob_IsNotTreatedAsExecutionFlowEvent`, `GetName_NativeJob_UsesHangfireJobDisplayName_WithArguments`, `HangfireValidationTests.DefaultHangfireJobName_Throws_ForNullIdGenerator`, `ServiceCollectionExtensionsTests.BothExtensions_ConsumerRegistryWins_ForJobNames`

- **FR-004 Dashboard integration**: `DashboardOptions.UseExecutionFlowJobNames(IHangfireJobName)` and `UseExecutionFlowJobNames(IServiceProvider)` set `DisplayNameFunc`. The provider overload resolves lazily. If no `IHangfireJobName` is registered, every job keeps its default name, with no exception and no warning. *(Story 1)*
  - **AC-004.1**: **Given** dashboard options, **When** `UseExecutionFlowJobNames` is called with an instance or a service provider, **Then** `DisplayNameFunc` is set (resolved lazily for the provider), and null options throw. Tests: `DashboardOptionsExtensionsTests.UseExecutionFlowJobNames_WithInstance_SetsDisplayNameFunc`, `UseExecutionFlowJobNames_WithServiceProvider_ResolvesLazily`, `UseExecutionFlowJobNames_NullOptions_Throws`
  - **AC-004.2**: **Given** a service provider with no `IHangfireJobName`, **When** a job is rendered, **Then** it gets its default name, with no exception. Test: `DashboardOptionsExtensionsTests.UseExecutionFlowJobNames_WithServiceProvider_FallsBackToDefaultName_WhenNotRegistered`

### Business Rules

- **RN-001** [CONFIRMED]: ExecutionFlow jobs are recognized by the dispatcher method (`HangfireJobDispatcher.DispatchEventAsync`/`DispatchRecurringAsync`), not by method shape.
- **RN-002** [CONFIRMED]: Hangfire's `[JobDisplayName]` only applies to methods, so on handlers it's read from `HandleAsync` (the method that runs the job), not from the class.
- **RN-003** [CONFIRMED]: The naming strategy can be replaced with `HangfireOptions.SetJobName<T>()`.
- **RN-004** [CONFIRMED]: On a producer-only host, handlers aren't registered, so names come from steps 4–5 (the event type).
- **RN-005** [CONFIRMED]: Step 5 (`GenerateId`) only applies when no name is defined anywhere. With the default generator, that name is the full type name, the same value used as the recurring job ID.
- **RN-006** [CONFIRMED]: There is a single fallback for every job, ExecutionFlow or not: `GenerateId(type)`, where the type is the registered handler type, otherwise the type the job carries (the event type for event jobs, the handler type for recurring jobs), otherwise `job.Type`. (`JobDisplayNameResolver.GetName`)
- **RN-007** [CONFIRMED]: The ordering rules live in one place, `JobDisplayNameResolver.GetConfiguredName`, which is protected and not virtual. A replacement `IHangfireJobName` can derive from `JobDisplayNameResolver` to reuse the rules and choose its own fallback.

### Key Entities

- **ICustomNameEvent**: an event that names its own job (core).
- **JobDisplayNameResolver**: the ExecutionFlow naming steps, one protected method each.
- **DefaultHangfireJobName**: the default `IHangfireJobName`, adding Hangfire's `[JobDisplayName]`.
- **HangfireJobInfo**: job facts (`CarriedType`, `Event`, `CustomJobName`, `GetHandler`); `null` for non-ExecutionFlow jobs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- None defined (the old spec had no NFRs). Outcomes are covered by the acceptance scenarios.

## Assumptions

- Out of scope: localizing names; names outside the dashboard (logging uses its own naming).
- Breaking and behavior changes in 1.2.0:
  - `ICustomNameEvent` moved from `ExecutionFlow.Hangfire` to `ExecutionFlow.Abstractions`.
  - `HangfireJobInfo.Create` returns `null` for non-ExecutionFlow jobs.
  - Non-ExecutionFlow jobs are no longer named after a type argument (generic jobs), and their `[JobDisplayName]` is honored.
  - Names that nothing defines now come from `IJobIdGenerator` (the full type name by default) instead of the class name. Native jobs without `[JobDisplayName]` show `GenerateId(job.Type)` instead of Hangfire's `Type.Method`.
  - A missing `IHangfireJobName` no longer breaks the dashboard.
  - New public base class `JobDisplayNameResolver`. `DefaultHangfireJobName` derives from it.
  - Removed from `HangfireJobInfo`, with the naming rules now in `JobDisplayNameResolver`: `GetExpectedName(IExecutionFlowRegistry)`, `GetExpectedName(IJobRegistryInfo)` and the protected `GetTypeDisplayName(Type)`.
  - `[JobDisplayName]` on a handler now comes after every configured name, including the event's `[DisplayName]`.
- Open questions: none. `{0}` in `[JobDisplayName]` = the event ([research.md](research.md) R-001, was plan DEC-001).
