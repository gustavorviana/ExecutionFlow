---
description: "Tasks for handler configuration classes (EF Core style)"
---

# Tasks: Handler configuration classes (EF Core style)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [quickstart.md](quickstart.md)

**Tests**: mandatory (constitution VII). Each story writes its tests first; a task is done when the tests of the acceptance scenarios it covers pass, together with the existing suite (SC-003).

**Order with 013**: implement 014 before 013. 014 creates `HandlerBuilder<T>` (replacing `ExecutionPlanEntry`) and its `RunMode(DependentRunMode, TimeSpan?)`; 013 then only adds `[DependentRun]` and the automatic plan on top (see 013 tasks).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

No setup: the projects and test projects already exist.

## Phase 2: Foundational (blocking)

- [ ] T001 Create `DependentRunMode` (`MinInterval`, `OwnSchedule`) in `Src/ExecutionFlow/Abstractions/DependentRunMode.cs` (shared with 013 FR-002)
- [ ] T002 Replace `ExecutionPlanEntry` with `HandlerBuilder` (non-generic, returned by `Add(Type)`) and `HandlerBuilder<THandler> : HandlerBuilder` (fluent methods return `HandlerBuilder<THandler>`) in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs`; delete `ExecutionPlanEntry.cs`; keep today's plan methods (`DependsOn`, `Enabled`, `DisplayName`, `NoOverlap`) and replace `MinInterval()`/`RunOnOwnSchedule()` with `RunMode(DependentRunMode, TimeSpan? interval = null)` ("interval positive; required for MinInterval, forbidden for OwnSchedule"); `ExecutionPlanner.Add<T>()`/`Add(Type)` return the builder (confirmed removal, spec Clarifications)
- [ ] T003 Migrate every caller to `HandlerBuilder` and `RunMode`: `Tests/ExecutionFlow.Tests/Abstractions/ExecutionPlannerTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/ExecutionPlanHangfireTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/PrerequisiteGateTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/RecurringRegistrationTests.cs`, `Tests/ExecutionFlow.Tests/Attributes/OverlapOptionTests.cs`, `Examples/ExecutionFlow.Examples.Handlers/Recurring/Plan/CatalogPlan.cs`, README; update the renamed test names in `specs/010-execution-plan/spec.md` AC-007.1..AC-007.4 with a one-line amendment note (depends on T002)

**Checkpoint**: the build and the whole suite are green with `HandlerBuilder` and `RunMode`; no new behavior yet.

## Phase 3: User Story 1 - Configure a handler in a dedicated class (Priority: P1) 🎯 MVP

**Goal**: `IHandlerConfiguration<T>` and the full provider-neutral builder.

**Independent Test**: a `ProductSyncConfiguration` sets `Cron`, `Id`, `DependsOn`, `RunMode` and `NoOverlap`; after `ApplyConfiguration` and `Build`, the step and the Hangfire registration reflect every value.

- [ ] T004 [P] [US1] Tests `HandlerConfigurationTests.Apply_SetsEveryValue`, `Interval_ReplacesAttributeCron`, `TimeZone_Throws_WhenNullEmptyOrUnknown`, `Apply_Throws_WhenTypeIsNotRecurringHandler`, `Apply_Throws_WhenHandlerConfiguredTwice`, `InlineAdd_ReturnsHandlerBuilder` in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs` (AC-001.1..AC-001.6)
- [ ] T005 [US1] Create `IHandlerConfiguration<THandler>` (`void Configure(HandlerBuilder<THandler> handler)`, `THandler : class, IHandler`) in `Src/ExecutionFlow/Abstractions/IHandlerConfiguration.cs` and `ExecutionPlanner.ApplyConfiguration<T>(IHandlerConfiguration<T>)` = `configuration.Configure(Add<T>())` (FR-001; depends on T002)
- [ ] T006 [US1] Add the explicit-value methods to `HandlerBuilder`: `Cron(string)` / `Interval(TimeSpan)` ("one clears the other; interval ≥ 1 min"), `Id(string)` ("non-empty"), `TimeZone(string)` ("null/empty throws"), `HasSetting(string key, object value)`; and to `ExecutionPlanStep` the explicit values `Cron`, `Interval`, `Id`, `TimeZone`, `ConfiguredEnabled` (bool?), `Settings` (read-only) (FR-001, R-001, R-004; depends on T005)
- [ ] T007 [US1] Hangfire item resolution: `RecurringJobResolver` reads the step first for schedule (cron or interval, 012 rules), Id and time zone, then `SetJobTimeZone<T>`, then the registry (attribute), then the global default; an unknown builder time zone throws at `Configure` naming the handler, in `Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs` and `HangfireSetup.cs` (FR-001, FR-002; depends on T006)

**Checkpoint**: MVP — a handler is fully configured by a class or inline, and Hangfire registers it.

## Phase 4: User Story 2 - Override attributes item by item (Priority: P1)

**Goal**: builder > per-handler option > attribute > global, per item, with merge/remove for dependencies, `AllowOverlap()`, and the warning when the builder overrides an option.

**Independent Test**: `[Recurring(cron, Id)]` + `[NoOverlap(300)]` with a configuration that only calls `NoOverlap(10 min)` keeps the attribute's cron and Id.

- [ ] T008 [P] [US2] Tests `HandlerConfigurationTests.Builder_ChangesOnlyWhatItSets`, `Dependencies_Merge`, `RemoveDependency_DropsAttributeDependency`, `ClearDependencies_DropsAll`, `AllowOverlap_TurnsOverlapBackOn`, `RemoveDependency_IsNoOp_WhenNotDeclared`, `ClearThenDependsOn_KeepsOnlyNewDependency` in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs`; `HandlerConfigurationHangfireTests.Builder_WinsOverPerHandlerOption_WithWarning`, `PerHandlerOption_Applies_WhenBuilderDoesNotSetItem`, `OptionTurningOffPrerequisite_FollowsPlanPolicy`, `Builder_WinsOverAttribute_ForIdTimeZoneAndName`, `AllowOverlap_DoesNotLetDependentOverlap` in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs` (AC-002.1..AC-002.11)
- [ ] T009 [US2] `RemoveDependency<X>()` / `ClearDependencies()` (the attribute's `[DependsOn]` is loaded first so they can drop it) and `AllowOverlap()` with three-state overlap ("not set / timeout / allowed", R-002) in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs` and `ExecutionPlanner.cs` (FR-002; depends on T006)
- [ ] T010 [US2] `ExecutionPlan` records the `MissingPrerequisite` policy; add an internal re-evaluation of availability given each step's enabled state (`ConfiguredEnabled` ?? provider's on/off), in `Src/ExecutionFlow/Abstractions/ExecutionPlan.cs` and `ExecutionPlanner.cs` (R-003; depends on T006)
- [ ] T011 [US2] Hangfire: `HangfireOptions.OnPlanRegistering` and `SetJobAutoRun` stop throwing for handlers in the plan; enabled = `ConfiguredEnabled` ?? `SetJobAutoRun<T>` ?? `GlobalRecurringAutoRun`, then re-evaluate availability (T010); `HangfireSetup.OnConfigured` emits a trace warning for each step whose builder value overrides `SetJobAutoRun<T>` or `SetJobTimeZone<T>`; turn the two tests that asserted the old error (`ExecutionPlanHangfireTests.UsePlan_Throws_WhenHandlerAlreadyHasSetJobAutoRun`, `SetJobAutoRun_Throws_WhenHandlerIsInPlan`) into precedence tests (FR-002, SC-003; depends on T007, T010)

## Phase 5: User Story 3 - Apply all configurations at once (Priority: P2)

**Goal**: `ApplyConfigurationsFromAssembly`.

**Independent Test**: an assembly with three configuration classes yields three configured steps.

- [ ] T012 [P] [US3] Tests in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs`: `HandlerConfigurationTests.ApplyFromAssembly_AppliesEveryConfiguration`, `ApplyFromAssembly_SkipsAbstract_ThrowsWithoutParameterlessConstructor`, `ApplyFromAssembly_CoexistsWithInlineAdd` (AC-003.1..AC-003.3); keep the test configuration classes `abstract` or in a nested scope where another scan would pick them up
- [ ] T013 [US3] `ExecutionPlanner.ApplyConfigurationsFromAssembly(Assembly)`: non-abstract classes implementing `IHandlerConfiguration<>`, parameterless constructor required (throws naming the class), applied through `ApplyConfiguration` (FR-003; depends on T005)

## Phase 6: User Story 4 - Provider-specific settings (Priority: P3)

**Goal**: Hangfire builder extensions for queue and retries.

**Independent Test**: `handler.Queue("reports")` makes the recurring job use the `reports` queue.

- [ ] T014 [P] [US4] Tests in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs`: `HandlerConfigurationHangfireTests.Queue_WinsOverAttribute`, `Retry_WinsOverAttributeAndOption` (AC-004.1, AC-004.2)
- [ ] T015 [US4] `HandlerBuilderExtensions.Queue(...)` and `.AutomaticRetry(int attempts, params int[] delaysInSeconds)` storing Hangfire filter instances under `Hangfire:`-prefixed keys in `Src/ExecutionFlow.Hangfire/HandlerBuilderExtensions.cs`; `HandlerJobFilterProvider` adds the step's filters and drops the handler's attribute filters of the same type, and a builder retry also replaces `DisableRecurringRetries` (FR-004; depends on T006)

## Phase 7: Polish & Cross-Cutting

- [ ] T016 [P] README: configuration classes (inline and class), the precedence table per item, the warnings, Hangfire extensions; the `[Timeout]` example is already removed (R-005)
- [ ] T017 [P] Amendment notes: one-line notes under 005 FR-003, FR-008, FR-009; 010 FR-001; 012 FR-004; 013 FR-002, pointing to 014; and update `specs/013-dependencies-by-attribute/tasks.md` so 013 builds on `HandlerBuilder` (drop its enum/`RunMode`/migration tasks already done here)
- [ ] T018 Run `quickstart.md` scenarios 1-6 and `/speckit-analyze`

## Dependencies & Execution Order

- T001 → T002 → T003 (green build) → T005 → T006 → T007 → T009, T010 → T011; T013 after T005; T015 after T006 → T016..T018
- Story order: US1 (MVP) → US2 → US3 → US4. US3 and US4 only need T005/T006 and can run in parallel with US2.

## Parallel Opportunities

- Test files T004, T008, T012, T014 can be written in parallel once T002 compiles.
- US3 (T013) and US4 (T015) in parallel with US2.
- T016 and T017 in parallel at the end.

## Implementation Strategy

1. Foundation: T001..T003 (rename and migrate, green build).
2. MVP: T004..T007.
3. Precedence and warnings: T008..T011.
4. Assembly scan and Hangfire extensions: T012..T015.
5. Docs, amendments and validation: T016..T018.
