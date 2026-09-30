# Spec: Setup and configuration

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`ExecutionFlowSetup`, `ExecutionFlowOptions`), `ExecutionFlow.Hangfire` (`HangfireSetup`, `HangfireOptions`, `FlowEngineJobActivator`), `ExecutionFlow.Hangfire.DependencyInjection` (`ServiceCollectionExtensions`)

## Context
Before anything runs, an app declares its handlers and options, then builds ExecutionFlow either in **full mode** (a consumer that runs handlers: filters, recurring jobs, dispatcher) or in **producer-only mode** (publish, query and cancel, with no global state). This can be done with or without Microsoft DI.

Related: [ADR-0005](../adr/ADR-0005-producer-only-mode.md) (producer-only), [ADR-0007](../adr/ADR-0007-multiple-processors.md) (processors other than Hangfire).

## Actors
| Actor | Description |
| ----- | ----------- |
| Consumer app | Runs handlers: full mode. |
| Producer app | Publishes, and queries or cancels jobs: producer-only mode. |
| App without DI | Uses `HangfireSetup` and `FlowEngineJobActivator` directly. |

## User stories
- US-001: As an app, I want to register handlers by assembly scan or one by one, with configuration errors reported at startup.
- US-002: As a consumer app, I want one call that wires ExecutionFlow into Hangfire and DI.
- US-003: As a producer app, I want to publish and manage jobs without affecting another Hangfire instance in the same process.
- US-004: As an app without DI, I want ExecutionFlow to activate handlers and their dependencies itself.

## Requirements

### REQ-001: Configure
`ExecutionFlowSetup<TOptions>.Configure(callback)` runs the callback, locks the options, publishes the handler registrations, and calls `OnConfigured` (validation).
Source: US-001

Acceptance criteria:
- AC-001.1: Registration by `Add` and `Scan` (with an optional predicate). Abstract types and interfaces are skipped. Tests: `ExecutionFlowOptionsTests.Scan_Populates_Registrations`, `Add_RecurringHandler_Populates_RecurringHandlers`, `Add_EventHandler_Populates_EventHandlers`, `Scan_WithPredicate_OnlyRegistersMatchingTypes`, `Scan_WithPredicate_ExcludesNonMatchingTypes`, `Scan_WithNullPredicate_RegistersAll`, `HangfireSetupTests.Configure_Scan_Finds_All_Handlers`, `Configure_Can_Mix_Scan_And_Add`, `ValidationTests.Scan_IgnoresNonHandlerTypes`
- AC-001.2: Invalid registrations throw. Tests: `ValidationTests.Add_Throws_ForNullType`, `Add_Throws_ForTypeThatDoesNotImplementIHandler`, `Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`, `Add_Throws_WhenTypeImplementsMultipleGenericIHandlers`, `Add_Throws_WhenDuplicateEventHandler_ForSameEventType`, `Add_AllowsReregistering_SameHandler`
- AC-001.3: After `Configure`, every option change throws, through methods (`Add`, `Scan`, `Set*`, `AddStateHandler`, `AddLogger`) and property setters alike. Options are a fixed snapshot. Tests: `ExecutionFlowOptionsTests.Scan_Throws_After_Lock`, `Add_Throws_After_Lock`, `HangfireOptionsTests.SetJobAutoRun_Throws_After_Lock`, `HangfireSetupTests.HangfireOptions_PropertySetters_Throw_AfterConfigure`, `HangfireOptions_PropertySetters_Work_InsideConfigure`
- AC-001.4: A partially loadable assembly: `Scan` registers the types that loaded and reports the failure through `OnTypeLoadFailure`. Tests: `AssemblyTypeScanContextTests.Constructor_Stores_Assembly`, `Constructor_Stores_Exception`, `Constructor_Stores_LoadedTypes`, `Exception_IsNull_WhenNotProvided` (the scan path itself is untested, see F-007)

### REQ-002: Full build
`HangfireSetup.Build(client?, storage?, provider?)`, once per instance. It defaults to `JobStorage.Current` and a new `BackgroundJobClient`. With no provider, it uses **the setup's own** `FlowEngineJobActivator`, the same one `ConfigureActivator()` installs, in either order, and never another setup's. It registers its global filters, **replacing those of the previously built full setup**, and registers the recurring jobs.
Source: US-002, US-004

Acceptance criteria:
- AC-002.1: Tests: `HangfireSetupTests.Build_Returns_NonNull_Dispatcher`, `Build_Throws_When_Called_Twice`, `Build_WithCustomServiceProvider_Uses_It`, `HangfireSetupGlobalFiltersTests.Build_RegistersDeduplicationCleanupFilter_Once`, `Build_RegistersStateFilter_AfterHangfireRetryFilter`
- AC-002.2: WHEN a second full `Build()` runs in the same process, THE SYSTEM SHALL leave exactly one set of ExecutionFlow filters: the last setup's. Test: `HangfireSetupGlobalStateTests.SecondFullBuild_ReplacesTheFirstSetupsGlobalFilters`
- AC-002.3: `Build()` without a provider SHALL NOT register anything into another setup's activator. Test: `HangfireSetupGlobalStateTests.Build_WithoutProvider_NeverUsesAnotherSetupsActivator`
- AC-002.4: `ConfigureActivator()` SHALL install the activator that `Build()` uses, so jobs resolve the setup's dispatcher and execution manager. Test: `HangfireSetupGlobalStateTests.ConfigureActivator_InstallsTheActivatorUsedByBuild`

### REQ-003: Producer-only build
`BuildDispatcherOnly(storage)` / `BuildDispatcherOnly(client, storage, provider?)`, once per instance, with non-null client and storage. It registers no global filters, no activator and no recurring jobs (P-005), and returns an `IEventDispatcher`. `HangfireSetup.ExecutionManager` is available afterwards, bound to the same storage.
Source: US-003

Acceptance criteria:
- AC-003.1: Tests: `HangfireSetupTests.BuildDispatcherOnly_Returns_NonNull_Dispatcher`, `BuildDispatcherOnly_Throws_When_Called_Twice`, `BuildDispatcherOnly_Throws_ForNullJobClient`, `BuildDispatcherOnly_Throws_ForNullStorage`, `BuildDispatcherOnly_WithStorageOnly_Returns_Dispatcher`, `BuildDispatcherOnly_CanPublish`, `HangfireSetupGlobalFiltersTests.BuildDispatcherOnly_DoesNotRegisterDeduplicationCleanupFilter`
- AC-003.2: Test: `HangfireSetupTests.BuildDispatcherOnly_ExposesExecutionManager`

### REQ-004: Built-in activator (no DI)
`FlowEngineJobActivator` activates jobs and their dependencies. Registered types are singletons, other concrete types are transient, interfaces and abstract types must be registered, and a single public constructor is required.
Source: US-004

Acceptance criteria:
- AC-004.1: Tests: `FlowEngineJobActivatorTests.DefaultRegistrations_JobActivator_Returns_Self`, `DefaultRegistrations_IServiceProvider_Returns_Self`, `DefaultRegistrations_Registry_Returns_Registry`, `GetService_UnregisteredConcreteType_CreatesInstance`, `GetService_UnregisteredConcreteType_CreatesNewInstanceEachTime`, `AddSingleton_WithInstance_ReturnsExactInstance`, `AddSingleton_WithType_CreatesSingleton`, `AddSingleton_WithFunc_LazyEvaluatesOnce`, `GetService_Interface_WithoutRegistration_Throws`, `GetService_Abstract_WithoutRegistration_Throws`, `ActivateJob_DelegatesToGetService`, `Constructor_Injection_ResolvesParameters`, `RegisterLoggerFactory_CreatesExecutionLoggerFactory`, `HangfireSetupTests.ConfigureActivator_Sets_JobActivator_Current`

### REQ-005: Microsoft DI
- `AddHangfireToExecutionFlow(configure?)` (full mode) registers handlers and hooks (transient), option values, logger factories, `IJobIdGenerator`, `IExecutionFlowRegistry`, `IHangfireJobName`, `HangfireJobDispatcher`, `IHangfireDispatcher`/`IEventDispatcher`/`IRecurringTrigger`, `IExecutionManager` (the setup's), and `ExecutionFlowStartupService`, which calls `StartExecutionFlow()` at host start.
- `AddExecutionFlowDispatcher(storage?, configure?)` (producer-only) registers the dispatcher and an `IExecutionManager` bound to the same storage.
- `IServiceProvider.StartExecutionFlow()` builds ExecutionFlow now, for apps without a generic host.
- The two modes are mutually exclusive in one container.

Source: US-002, US-003

Acceptance criteria:
- AC-005.1: Tests: `ServiceCollectionExtensionsTests.Registers_IDispatcher`, `Registers_IExecutionManager`, `Registers_IHangfireJobName`, `Registers_IExecutionFlowRegistry`, `Registers_IRecurringTrigger`, `WithoutOptions_DoesNotThrow`, `ScansAssembly_RegistersDiscoveredHandlers`, `Registers_EventHandler_AsTransient`, `Registers_RecurringHandler_AsTransient`, `Registers_HangfireJobDispatcher`, `HangfireJobDispatcher_UsesConfiguredRegistry_EvenWhenAnotherRegistryWinsInContainer`, `DispatcherOnly_DefaultOverload_ResolvesStorageFromDI`, `DispatcherOnly_WithStorageFunc_Registers_IEventDispatcher`, `DispatcherOnly_DoesNotRegister_IRecurringTrigger`, `DispatcherOnly_WithStorageFunc_CanPublish`, `ExecutionManager_IsBoundToTheBuiltSetup`
- AC-005.2: WHEN both modes are registered in one container, in either order, THE SYSTEM SHALL throw `InvalidOperationException` naming the call to keep. Tests: `ServiceCollectionExtensionsTests.BothExtensions_Throw_WhenProducerOnlyIsRegisteredFirst`, `BothExtensions_Throw_WhenFullModeIsRegisteredFirst`
- AC-005.3: `StartExecutionFlow()` SHALL build ExecutionFlow without a host. Test: `ServiceCollectionExtensionsTests.StartExecutionFlow_BuildsWithoutAHost`
- AC-005.4: Producer-only SHALL register `IExecutionManager`. Test: `ServiceCollectionExtensionsTests.DispatcherOnly_Registers_IExecutionManager`

## Business rules
- RN-001 [CONFIRMED] `Build` and `BuildDispatcherOnly` can be called once per `HangfireSetup` (together, not each).
- RN-002 [CONFIRMED] One active **full Hangfire** setup per process: the last full `Build()` wins, and the previous one keeps publishing but its hooks and handler filters stop. Producer-only setups are unlimited. This limit comes from Hangfire's process-wide statics. It is **not** an ExecutionFlow-wide rule: other processors, and several setups of them, aren't limited by it (ADR-0007).
- RN-003 [CONFIRMED] `Configure` is optional with DI. Without `Configure`, `OnConfigured` validation never runs.
- RN-004 [CONFIRMED] Producer-only limits (documented): a job cancelled from a producer-only host fires no hooks, and its deduplication reservation key is released on the next publish of that custom ID (stale key), not immediately.
- RN-005 [CONFIRMED] The DI package requires `Microsoft.Extensions.*.Abstractions` >= 6.0.0. Shipped libraries declare pinned lower bounds (constitution).

## Out of scope
- Processors other than Hangfire (ADR-0007).
- Several full Hangfire setups in one process, e.g. per-storage filters and activators (ADR-0007, possible evolution).

## Breaking and behavior changes in 1.2.0
- Option property setters throw after `Configure`.
- Registering both `AddHangfireToExecutionFlow` and `AddExecutionFlowDispatcher` throws.
- A second full `Build()` replaces the first one's filters instead of adding more.
- `Build()` no longer reuses `JobActivator.Current` from another setup.
- New: `HangfireSetup.ExecutionManager`, `IServiceProvider.StartExecutionFlow()`, and `IExecutionManager` in producer-only.
- The DI package's minimum `Microsoft.Extensions.*` dependency dropped from 10.0 to 6.0.

## Open questions
- None.
