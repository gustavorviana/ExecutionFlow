# Feature Specification: Setup and configuration

**Feature Branch**: `main` (brownfield capability)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: to-be (v1.2.0), based on the as-is mapping (findings in [research.md](research.md))

**Input**: Migrated from `docs/specs/setup-and-configuration/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept).

**Packages**: `ExecutionFlow` (`ExecutionFlowSetup`, `ExecutionFlowOptions`), `ExecutionFlow.Hangfire` (`HangfireSetup`, `HangfireOptions`, `FlowEngineJobActivator`), `ExecutionFlow.Hangfire.DependencyInjection` (`ServiceCollectionExtensions`)

**Amends**: none

## Context

Before anything runs, an app declares its handlers and options, then builds ExecutionFlow either in **full mode** (a consumer that runs handlers: filters, recurring jobs, dispatcher) or in **producer-only mode** (publish, query and cancel, with no global state). This can be done with or without Microsoft DI.

Related: [research.md](research.md) R-002 (producer-only, was ADR-0005), [constitution](../../.specify/memory/constitution.md) principle II (processors other than Hangfire, was ADR-0007).

**Actors**: the consumer app (runs handlers: full mode), the producer app (publishes, and queries or cancels jobs: producer-only mode), and the app without DI (uses `HangfireSetup` and `FlowEngineJobActivator` directly).

## Clarifications

### Session 2026-11-03

- Q: What is the built-in activator for? → A: Internal use only, a fallback for apps that don't use Microsoft DI; it isn't meant to be the app's DI. The documentation of `HangfireSetup` recommends Microsoft DI (`AddHangfireToExecutionFlow`) (maintainer, 2026-11-03).
- Q: Restrict what the activator creates (an "ExecutionFlow only" mode)? → A: No. Only make it reliable, without changing what it creates or the lifetimes of FR-004 (maintainer, 2026-11-03). Compatible change: no version bump beyond the normal one (constitution V).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register handlers with startup validation (Priority: P1)

As an app, I want to register handlers by assembly scan or one by one, with configuration errors reported at startup.

**Why this priority**: every other feature needs registered handlers.

**Independent Test**: configure options with `Add` and `Scan`, including invalid types, and check the registrations and exceptions.

**Acceptance Scenarios**: AC-001.1..AC-001.4.

---

### User Story 2 - One call wires ExecutionFlow into Hangfire and DI (Priority: P1)

As a consumer app, I want one call that wires ExecutionFlow into Hangfire and DI.

**Why this priority**: it is the main entry point for consumer apps.

**Independent Test**: call `AddHangfireToExecutionFlow`, resolve the services, and build; or call `HangfireSetup.Build` directly.

**Acceptance Scenarios**: AC-002.1..AC-002.4, AC-005.1..AC-005.3.

---

### User Story 3 - Producer-only without side effects (Priority: P2)

As a producer app, I want to publish and manage jobs without affecting another Hangfire instance in the same process.

**Why this priority**: needed for split API/worker architectures, but not for single-process apps.

**Independent Test**: `BuildDispatcherOnly` / `AddExecutionFlowDispatcher`, publish, and check no global filters were registered and an `IExecutionManager` is available.

**Acceptance Scenarios**: AC-003.1, AC-003.2, AC-005.2, AC-005.4.

---

### User Story 4 - Built-in activator without DI (Priority: P2)

As an app without DI, I want ExecutionFlow to activate handlers and their dependencies itself.

**Why this priority**: supports apps without a container; DI apps don't need it.

**Independent Test**: resolve registered and unregistered types from `FlowEngineJobActivator`.

**Acceptance Scenarios**: AC-004.1, AC-002.3, AC-002.4.

### Edge Cases

- A partially loadable assembly: `Scan` registers the types that loaded and reports the failure through `OnTypeLoadFailure` (AC-001.4).
- A second full `Build()` in the same process replaces the first one's filters (AC-002.2, RN-002).
- Both DI modes registered in one container, in either order: throws (AC-005.2).
- No generic host: `StartExecutionFlow()` builds ExecutionFlow (AC-005.3).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Configure**: `ExecutionFlowSetup<TOptions>.Configure(callback)` runs the callback, locks the options, publishes the handler registrations, and calls `OnConfigured` (validation). *(Story 1)*
  - **AC-001.1**: **Given** handlers registered by `Add` or `Scan` (with an optional predicate), **When** options are configured, **Then** registrations are populated, and abstract types, interfaces and non-handler types are skipped. Tests: `ExecutionFlowOptionsTests.Scan_Populates_Registrations`, `ExecutionFlowOptionsTests.Add_RecurringHandler_Populates_RecurringHandlers`, `ExecutionFlowOptionsTests.Add_EventHandler_Populates_EventHandlers`, `ExecutionFlowOptionsTests.Scan_WithPredicate_OnlyRegistersMatchingTypes`, `ExecutionFlowOptionsTests.Scan_WithPredicate_ExcludesNonMatchingTypes`, `ExecutionFlowOptionsTests.Scan_WithNullPredicate_RegistersAll`, `HangfireSetupTests.Configure_Scan_Finds_All_Handlers`, `HangfireSetupTests.Configure_Can_Mix_Scan_And_Add`, `ValidationTests.Scan_IgnoresNonHandlerTypes`
  - **AC-001.2**: **Given** an invalid registration, **When** it is added, **Then** it throws (re-registering the same handler is allowed). Tests: `ValidationTests.Add_Throws_ForNullType`, `ValidationTests.Add_Throws_ForTypeThatDoesNotImplementIHandler`, `ValidationTests.Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`, `ValidationTests.Add_Throws_WhenTypeImplementsMultipleGenericIHandlers`, `ValidationTests.Add_Throws_WhenDuplicateEventHandler_ForSameEventType`, `ValidationTests.Add_AllowsReregistering_SameHandler`
  - **AC-001.3**: **Given** options after `Configure`, **When** any option is changed, through methods (`Add`, `Scan`, `Set*`, `AddStateHandler`, `AddLogger`) or property setters alike, **Then** it throws; options are a fixed snapshot. Tests: `ExecutionFlowOptionsTests.Scan_Throws_After_Lock`, `ExecutionFlowOptionsTests.Add_Throws_After_Lock`, `HangfireOptionsTests.SetJobAutoRun_Throws_After_Lock`, `HangfireSetupTests.HangfireOptions_PropertySetters_Throw_AfterConfigure`, `HangfireSetupTests.HangfireOptions_PropertySetters_Work_InsideConfigure`
  - **AC-001.4**: **Given** a partially loadable assembly, **When** it is scanned, **Then** `Scan` registers the types that loaded and reports the failure through `OnTypeLoadFailure`. Tests: `AssemblyTypeScanContextTests.Constructor_Stores_Assembly`, `AssemblyTypeScanContextTests.Constructor_Stores_Exception`, `AssemblyTypeScanContextTests.Constructor_Stores_LoadedTypes`, `AssemblyTypeScanContextTests.Exception_IsNull_WhenNotProvided` (the scan path itself is untested, see [research.md](research.md) F-007)

- **FR-002 Full build**: `HangfireSetup.Build(client?, storage?, provider?)`, once per instance. It defaults to `JobStorage.Current` and a new `BackgroundJobClient`. With no provider, it uses **the setup's own** `FlowEngineJobActivator`, the same one `ConfigureActivator()` installs, in either order, and never another setup's. It registers its global filters, **replacing those of the previously built full setup**, and registers the recurring jobs. *(Stories 2, 4)*
  - **AC-002.1**: **Given** a configured setup, **When** `Build` is called, **Then** it returns a dispatcher, throws on a second call, uses a custom provider if given, and registers the filters once and in order. Tests: `HangfireSetupTests.Build_Returns_NonNull_Dispatcher`, `HangfireSetupTests.Build_Throws_When_Called_Twice`, `HangfireSetupTests.Build_WithCustomServiceProvider_Uses_It`, `HangfireSetupGlobalFiltersTests.Build_RegistersDeduplicationCleanupFilter_Once`, `HangfireSetupGlobalFiltersTests.Build_RegistersStateFilter_AfterHangfireRetryFilter`
  - **AC-002.2**: **Given** a first full setup built, **When** a second full `Build()` runs in the same process, **Then** exactly one set of ExecutionFlow filters remains: the last setup's. Test: `HangfireSetupGlobalStateTests.SecondFullBuild_ReplacesTheFirstSetupsGlobalFilters`
  - **AC-002.3**: **Given** another setup's activator installed, **When** `Build()` runs without a provider, **Then** it registers nothing into that other activator. Test: `HangfireSetupGlobalStateTests.Build_WithoutProvider_NeverUsesAnotherSetupsActivator`
  - **AC-002.4**: **Given** `ConfigureActivator()` was called, **When** `Build()` runs, **Then** it uses the installed activator, so jobs resolve the setup's dispatcher and execution manager. Test: `HangfireSetupGlobalStateTests.ConfigureActivator_InstallsTheActivatorUsedByBuild`

- **FR-003 Producer-only build**: `BuildDispatcherOnly(storage)` / `BuildDispatcherOnly(client, storage, provider?)`, once per instance, with non-null client and storage. It registers no global filters, no activator and no recurring jobs (constitution principle IV, was P-005), and returns an `IEventDispatcher`. `HangfireSetup.ExecutionManager` is available afterwards, bound to the same storage. *(Story 3)*
  - **AC-003.1**: **Given** a configured setup, **When** `BuildDispatcherOnly` is called, **Then** it returns a dispatcher that can publish, throws on a second call or null client/storage, and registers no deduplication cleanup filter. Tests: `HangfireSetupTests.BuildDispatcherOnly_Returns_NonNull_Dispatcher`, `HangfireSetupTests.BuildDispatcherOnly_Throws_When_Called_Twice`, `HangfireSetupTests.BuildDispatcherOnly_Throws_ForNullJobClient`, `HangfireSetupTests.BuildDispatcherOnly_Throws_ForNullStorage`, `HangfireSetupTests.BuildDispatcherOnly_WithStorageOnly_Returns_Dispatcher`, `HangfireSetupTests.BuildDispatcherOnly_CanPublish`, `HangfireSetupGlobalFiltersTests.BuildDispatcherOnly_DoesNotRegisterDeduplicationCleanupFilter`
  - **AC-003.2**: **Given** a producer-only build, **When** `HangfireSetup.ExecutionManager` is read, **Then** it is available. Test: `HangfireSetupTests.BuildDispatcherOnly_ExposesExecutionManager`

- **FR-004 Built-in activator (no DI)**: `FlowEngineJobActivator` activates jobs and their dependencies. Registered types are singletons, other concrete types are transient, interfaces and abstract types must be registered, and a single public constructor is required. *(Story 4)*
  - **AC-004.1**: **Given** a `FlowEngineJobActivator`, **When** registered and unregistered types are resolved, **Then** it follows those lifetime and registration rules. Tests: `FlowEngineJobActivatorTests.DefaultRegistrations_JobActivator_Returns_Self`, `FlowEngineJobActivatorTests.DefaultRegistrations_IServiceProvider_Returns_Self`, `FlowEngineJobActivatorTests.DefaultRegistrations_Registry_Returns_Registry`, `FlowEngineJobActivatorTests.GetService_UnregisteredConcreteType_CreatesInstance`, `FlowEngineJobActivatorTests.GetService_UnregisteredConcreteType_CreatesNewInstanceEachTime`, `FlowEngineJobActivatorTests.AddSingleton_WithInstance_ReturnsExactInstance`, `FlowEngineJobActivatorTests.AddSingleton_WithType_CreatesSingleton`, `FlowEngineJobActivatorTests.AddSingleton_WithFunc_LazyEvaluatesOnce`, `FlowEngineJobActivatorTests.GetService_Interface_WithoutRegistration_Throws`, `FlowEngineJobActivatorTests.GetService_Abstract_WithoutRegistration_Throws`, `FlowEngineJobActivatorTests.ActivateJob_DelegatesToGetService`, `FlowEngineJobActivatorTests.Constructor_Injection_ResolvesParameters`, `FlowEngineJobActivatorTests.RegisterLoggerFactory_CreatesExecutionLoggerFactory`, `HangfireSetupTests.ConfigureActivator_Sets_JobActivator_Current`
  - **AC-004.2**: **Given** types whose constructors depend on each other in a cycle, **When** one of them is resolved, **Then** the activator throws `InvalidOperationException` naming the chain, instead of overflowing the stack and ending the process. Test: `FlowEngineJobActivatorTests.GetService_ConstructorCycle_ThrowsNamingTheChain`
  - **AC-004.3**: **Given** a registered type resolved by many threads at the same time, **When** its singleton is created, **Then** its constructor runs once and every caller gets the same instance. Tests: `FlowEngineJobActivatorTests.AddSingleton_WithType_CreatesOnce_WhenResolvedConcurrently`, `FlowEngineJobActivatorTests.AddSingleton_WithFunc_RunsOnce_WhenResolvedConcurrently`

- **FR-005 Microsoft DI**: *(Stories 2, 3)*
  - `AddHangfireToExecutionFlow(configure?)` (full mode) registers handlers and hooks (transient), option values, logger factories, `IJobIdGenerator`, `IExecutionFlowRegistry`, `IHangfireJobName`, `HangfireJobDispatcher`, `IHangfireDispatcher`/`IEventDispatcher`/`IRecurringTrigger`, `IExecutionManager` (the setup's), and `ExecutionFlowStartupService`, which calls `StartExecutionFlow()` at host start.
  - `AddExecutionFlowDispatcher(storage?, configure?)` (producer-only) registers the dispatcher and an `IExecutionManager` bound to the same storage.
  - `IServiceProvider.StartExecutionFlow()` builds ExecutionFlow now, for apps without a generic host.
  - The two modes are mutually exclusive in one container.
  - **AC-005.1**: **Given** a service collection, **When** either extension is called and services are resolved, **Then** the listed services are registered with the documented lifetimes and bindings. Tests: `ServiceCollectionExtensionsTests.Registers_IDispatcher`, `ServiceCollectionExtensionsTests.Registers_IExecutionManager`, `ServiceCollectionExtensionsTests.Registers_IHangfireJobName`, `ServiceCollectionExtensionsTests.Registers_IExecutionFlowRegistry`, `ServiceCollectionExtensionsTests.Registers_IRecurringTrigger`, `ServiceCollectionExtensionsTests.WithoutOptions_DoesNotThrow`, `ServiceCollectionExtensionsTests.ScansAssembly_RegistersDiscoveredHandlers`, `ServiceCollectionExtensionsTests.Registers_EventHandler_AsTransient`, `ServiceCollectionExtensionsTests.Registers_RecurringHandler_AsTransient`, `ServiceCollectionExtensionsTests.Registers_HangfireJobDispatcher`, `ServiceCollectionExtensionsTests.HangfireJobDispatcher_UsesConfiguredRegistry_EvenWhenAnotherRegistryWinsInContainer`, `ServiceCollectionExtensionsTests.DispatcherOnly_DefaultOverload_ResolvesStorageFromDI`, `ServiceCollectionExtensionsTests.DispatcherOnly_WithStorageFunc_Registers_IEventDispatcher`, `ServiceCollectionExtensionsTests.DispatcherOnly_DoesNotRegister_IRecurringTrigger`, `ServiceCollectionExtensionsTests.DispatcherOnly_WithStorageFunc_CanPublish`, `ServiceCollectionExtensionsTests.ExecutionManager_IsBoundToTheBuiltSetup`
  - **AC-005.2**: **Given** both modes registered in one container, in either order, **When** the second is registered, **Then** `InvalidOperationException` is thrown naming the call to keep. Tests: `ServiceCollectionExtensionsTests.BothExtensions_Throw_WhenProducerOnlyIsRegisteredFirst`, `ServiceCollectionExtensionsTests.BothExtensions_Throw_WhenFullModeIsRegisteredFirst`
  - **AC-005.3**: **Given** no generic host, **When** `StartExecutionFlow()` is called, **Then** ExecutionFlow is built. Test: `ServiceCollectionExtensionsTests.StartExecutionFlow_BuildsWithoutAHost`
  - **AC-005.4**: **Given** producer-only registration, **When** services are resolved, **Then** `IExecutionManager` is registered. Test: `ServiceCollectionExtensionsTests.DispatcherOnly_Registers_IExecutionManager`

### Business Rules

- **RN-001** [CONFIRMED]: `Build` and `BuildDispatcherOnly` can be called once per `HangfireSetup` (together, not each).
- **RN-002** [CONFIRMED]: One active **full Hangfire** setup per process: the last full `Build()` wins, and the previous one keeps publishing but its hooks and handler filters stop. Producer-only setups are unlimited. This limit comes from Hangfire's process-wide statics. It is **not** an ExecutionFlow-wide rule: other processors, and several setups of them, aren't limited by it ([constitution](../../.specify/memory/constitution.md) principle II, was ADR-0007).
- **RN-003** [CONFIRMED]: `Configure` is optional with DI. Without `Configure`, `OnConfigured` validation never runs.
- **RN-004** [CONFIRMED]: Producer-only limits (documented): a job cancelled from a producer-only host fires no hooks, and its deduplication reservation key is released on the next publish of that custom ID (stale key), not immediately.
- **RN-005** [CONFIRMED]: The DI package requires `Microsoft.Extensions.*.Abstractions` >= 6.0.0. Shipped libraries declare pinned lower bounds ([constitution](../../.specify/memory/constitution.md), Technical Constraints).

### Key Entities

- **ExecutionFlowSetup / ExecutionFlowOptions**: declare handlers and options; locked by `Configure`.
- **HangfireSetup / HangfireOptions**: Hangfire build in full or producer-only mode.
- **FlowEngineJobActivator**: built-in activator for apps without DI.
- **ServiceCollectionExtensions**: Microsoft DI entry points.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- None (the old spec had no NFRs). Behavior is measured by the AC tests above.

## Assumptions

- Out of scope: processors other than Hangfire (constitution principle II, was ADR-0007); several full Hangfire setups in one process, e.g. per-storage filters and activators (constitution principle II, possible evolution).
- Breaking and behavior changes in 1.2.0: option property setters throw after `Configure`; registering both `AddHangfireToExecutionFlow` and `AddExecutionFlowDispatcher` throws; a second full `Build()` replaces the first one's filters instead of adding more; `Build()` no longer reuses `JobActivator.Current` from another setup; new `HangfireSetup.ExecutionManager`, `IServiceProvider.StartExecutionFlow()`, and `IExecutionManager` in producer-only; the DI package's minimum `Microsoft.Extensions.*` dependency dropped from 10.0 to 6.0.
- Open questions: none.
