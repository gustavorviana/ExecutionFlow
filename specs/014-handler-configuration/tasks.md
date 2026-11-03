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

- [X] T001 Create `DependentRunMode` (`MinInterval`, `OwnSchedule`) in `Src/ExecutionFlow/Abstractions/DependentRunMode.cs` (shared with 013 FR-002)
- [X] T002 Replace `ExecutionPlanEntry` with `HandlerBuilder` (non-generic, returned by `Add(Type)`) and `HandlerBuilder<THandler> : HandlerBuilder` (fluent methods return `HandlerBuilder<THandler>`) in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs`; delete `ExecutionPlanEntry.cs`; keep today's plan methods (`DependsOn`, `Enabled`, `DisplayName`, `NoOverlap`) and replace `MinInterval()`/`RunOnOwnSchedule()` with `RunMode(DependentRunMode, TimeSpan? interval = null)` ("interval positive; required for MinInterval, forbidden for OwnSchedule"); `ExecutionPlanner.Add<T>()`/`Add(Type)` return the builder (confirmed removal, spec Clarifications)
- [X] T003 Migrate every caller to `HandlerBuilder` and `RunMode`: `Tests/ExecutionFlow.Tests/Abstractions/ExecutionPlannerTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/ExecutionPlanHangfireTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/PrerequisiteGateTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/RecurringRegistrationTests.cs`, `Tests/ExecutionFlow.Tests/Attributes/OverlapOptionTests.cs`, `Examples/ExecutionFlow.Examples.Handlers/Recurring/Plan/CatalogPlan.cs`, README; update the renamed test names in `specs/010-execution-plan/spec.md` AC-007.1..AC-007.4 with a one-line amendment note (depends on T002)

**Checkpoint**: the build and the whole suite are green with `HandlerBuilder` and `RunMode`; no new behavior yet.

## Phase 3: User Story 1 - Configure a handler in a dedicated class (Priority: P1) 🎯 MVP

**Goal**: `IHandlerConfiguration<T>` and the full provider-neutral builder.

**Independent Test**: a `ProductSyncConfiguration` sets `Cron`, `Id`, `DependsOn`, `RunMode` and `NoOverlap`; after `ApplyConfiguration` and `Build`, the step and the Hangfire registration reflect every value.

- [X] T004 [P] [US1] Tests `HandlerConfigurationTests.Apply_SetsEveryValue`, `Interval_ReplacesAttributeCron`, `TimeZone_Throws_WhenNullEmptyOrUnknown`, `Apply_Throws_WhenTypeIsNotRecurringHandler`, `Apply_Throws_WhenHandlerConfiguredTwice`, `InlineAdd_ReturnsHandlerBuilder` in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs` (AC-001.1..AC-001.6)
- [X] T005 [US1] Create `IHandlerConfiguration<THandler>` (`void Configure(HandlerBuilder<THandler> handler)`, `THandler : class, IHandler`) in `Src/ExecutionFlow/Abstractions/IHandlerConfiguration.cs` and `ExecutionPlanner.ApplyConfiguration<T>(IHandlerConfiguration<T>)` = `configuration.Configure(Add<T>())` (FR-001; depends on T002)
- [X] T006 [US1] Add the explicit-value methods to `HandlerBuilder`: `Cron(string)` / `Interval(TimeSpan)` ("one clears the other; interval ≥ 1 min"), `Id(string)` ("non-empty"), `TimeZone(string)` ("null/empty throws"), `HasSetting(string key, object value)`; and to `ExecutionPlanStep` the explicit values `Cron`, `Interval`, `Id`, `TimeZone`, `ConfiguredEnabled` (bool?), `Settings` (read-only) (FR-001, R-001, R-004; depends on T005)
- [X] T007 [US1] Hangfire item resolution: `RecurringJobResolver` reads the step first for schedule (cron or interval, 012 rules), Id and time zone, then `SetJobTimeZone<T>`, then the registry (attribute), then the global default; an unknown builder time zone throws at `Configure` naming the handler, in `Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs` and `HangfireSetup.cs` (FR-001, FR-002; depends on T006)

**Checkpoint**: MVP — a handler is fully configured by a class or inline, and Hangfire registers it.

## Phase 4: User Story 2 - Override attributes item by item (Priority: P1)

**Goal**: builder > per-handler option > attribute > global, per item, with merge/remove for dependencies, `AllowOverlap()`, and the warning when the builder overrides an option.

**Independent Test**: `[Recurring(cron, Id)]` + `[NoOverlap(300)]` with a configuration that only calls `NoOverlap(10 min)` keeps the attribute's cron and Id.

- [X] T008 [P] [US2] Tests `HandlerConfigurationTests.Builder_ChangesOnlyWhatItSets`, `Dependencies_Merge`, `RemoveDependency_DropsAttributeDependency`, `ClearDependencies_DropsAll`, `AllowOverlap_TurnsOverlapBackOn`, `RemoveDependency_IsNoOp_WhenNotDeclared`, `ClearThenDependsOn_KeepsOnlyNewDependency` in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs`; `HandlerConfigurationHangfireTests.Builder_WinsOverPerHandlerOption_WithWarning`, `PerHandlerOption_Applies_WhenBuilderDoesNotSetItem`, `OptionTurningOffPrerequisite_FollowsPlanPolicy`, `Builder_WinsOverAttribute_ForIdTimeZoneAndName`, `AllowOverlap_DoesNotLetDependentOverlap` in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs` (AC-002.1..AC-002.11)
- [X] T009 [US2] `RemoveDependency<X>()` / `ClearDependencies()` (the attribute's `[DependsOn]` is loaded first so they can drop it) and `AllowOverlap()` with three-state overlap ("not set / timeout / allowed", R-002) in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs` and `ExecutionPlanner.cs` (FR-002; depends on T006)
- [X] T010 [US2] `ExecutionPlan` records the `MissingPrerequisite` policy; add an internal re-evaluation of availability given each step's enabled state (`ConfiguredEnabled` ?? provider's on/off), in `Src/ExecutionFlow/Abstractions/ExecutionPlan.cs` and `ExecutionPlanner.cs` (R-003; depends on T006)
- [X] T011 [US2] Hangfire: `HangfireOptions.OnPlanRegistering` and `SetJobAutoRun` stop throwing for handlers in the plan; enabled = `ConfiguredEnabled` ?? `SetJobAutoRun<T>` ?? `GlobalRecurringAutoRun`, then re-evaluate availability (T010); `HangfireSetup.OnConfigured` emits a trace warning for each step whose builder value overrides `SetJobAutoRun<T>` or `SetJobTimeZone<T>`; turn the two tests that asserted the old error (`ExecutionPlanHangfireTests.UsePlan_Throws_WhenHandlerAlreadyHasSetJobAutoRun`, `SetJobAutoRun_Throws_WhenHandlerIsInPlan`) into precedence tests (FR-002, SC-003; depends on T007, T010)

## Phase 5: User Story 3 - Apply all configurations at once (Priority: P2)

**Goal**: `ApplyConfigurationsFromAssembly`.

**Independent Test**: an assembly with three configuration classes yields three configured steps.

- [X] T012 [P] [US3] Tests in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs`: `HandlerConfigurationTests.ApplyFromAssembly_AppliesEveryConfiguration`, `ApplyFromAssembly_SkipsAbstract_ThrowsWithoutParameterlessConstructor`, `ApplyFromAssembly_CoexistsWithInlineAdd` (AC-003.1..AC-003.3); keep the test configuration classes `abstract` or in a nested scope where another scan would pick them up
- [X] T013 [US3] `ExecutionPlanner.ApplyConfigurationsFromAssembly(Assembly)`: non-abstract classes implementing `IHandlerConfiguration<>`, parameterless constructor required (throws naming the class), applied through `ApplyConfiguration` (FR-003; depends on T005)

## Phase 6: User Story 4 - Provider-specific settings (Priority: P3)

**Goal**: Hangfire builder extensions for queue and retries.

**Independent Test**: `handler.Queue("reports")` makes the recurring job use the `reports` queue.

- [X] T014 [P] [US4] Tests in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs`: `HandlerConfigurationHangfireTests.Queue_WinsOverAttribute`, `Retry_WinsOverAttributeAndOption` (AC-004.1, AC-004.2)
- [X] T015 [US4] `HandlerBuilderExtensions.Queue(...)` and `.AutomaticRetry(int attempts, params int[] delaysInSeconds)` storing Hangfire filter instances under `Hangfire:`-prefixed keys in `Src/ExecutionFlow.Hangfire/HandlerBuilderExtensions.cs`; `HandlerJobFilterProvider` adds the step's filters and drops the handler's attribute filters of the same type, and a builder retry also replaces `DisableRecurringRetries` (FR-004; depends on T006)

## Phase 8: FR-005 shortcut (added 2026-11-02)

- [X] T019 [P] [US3] Tests `HandlerConfigurationTests.UsePlanFromAssembly_AppliesFilteredConfigurations`, `UsePlanFromAssembly_Throws_WhenPlanAlreadyRegistered`, `UsePlanFromAssembly_UsesPolicy` in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs` (AC-005.1..AC-005.3)
- [X] T020 [US3] `ExecutionFlowOptions.UsePlanFromAssembly(Assembly, Func<Type, bool> predicate = null, MissingPrerequisite policy = Throw)` in `Src/ExecutionFlow/ExecutionFlowOptions.cs`, plus a README note on option A: one plan per provider, chosen with the assembly/namespace filter (FR-005; depends on T019)

## Phase 9: FR-006 internal planner and the options layer (regenerated 2026-11-02 from the revised plan)

**Goal**: two planners per provider's options. The internal planner (class attributes + `options.GetPlanOf<T>()`) decides handlers outside the user plan and forms the automatic plan; a handler in the user plan drops its `GetPlanOf` values as a whole (one warning) and keeps its attributes per item. `SetJobAutoRun`/`SetJobTimeZone` become `[Obsolete]` shortcuts to `GetPlanOf`.

**Independent Test**: `GetPlanOf<T>().Enabled(false)` turns `T` off without a user plan; with `T` in a user plan that doesn't set `Enabled`, `T` runs and one warning names it.

- [X] T021 [US1] `HandlerBuilder` abstract with an internal constructor; `ExecutionPlanner.Add(Type)` creates a `HandlerBuilder<T>` internally, in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs` and `ExecutionPlanner.cs` (FR-006; done early on 2026-11-02; its test is written in T022)
- [X] T026 Spec text fixes from the analysis (done 2026-11-02, with AC-006.7 and the R-006..R-009 confirmations), in `specs/014-handler-configuration/spec.md`: rewrite AC-002.5 on the single warning rule of research R-006 (analysis D1); update the `Amends` line for 005 FR-003/FR-008 and the "Enabled"/"Time zone" rows of the precedence table to `GetPlanOf<T>()` (I1); SC-003 "three tests" (I2); status "Implemented (FR-001..FR-005); FR-006 planned" (S1)
- [X] T022 [P] [US2] Tests first: AC-006.1..AC-006.5 in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs` and `Tests/ExecutionFlow.Tests/AutomaticPlanTests.cs` (`GetPlanOf_AppliesWithoutUserPlan`, `UserPlan_ReplacesGetPlanOf_AsAWhole`, `UserPlan_KeepsAttributes_PerItem`, `ObsoleteOptions_RouteToGetPlanOf`, `GetPlanOf_DependsOn_FormsAutomaticPlan`), AC-006.6 in `Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs` (`HandlerBuilder_IsAbstract_WithNoPublicConstructor`), plus `HandlerConfigurationTests.GetPlanOf_Throws_WhenHandlerNotRegistered` (research R-007); convert the tests of the deprecated AC-002.6/AC-002.7 (`PerHandlerOption_Applies_WhenBuilderDoesNotSetItem`, `OptionTurningOffPrerequisite_FollowsPlanPolicy`, `ExecutionPlanHangfireTests.SetJobAutoRun_Applies_WhenBuilderDoesNotSetEnabled`) into AC-006.1/AC-006.2 cases
- [X] T023 [US2] Core internal planner in `Src/ExecutionFlow/ExecutionFlowOptions.cs`: private `ExecutionPlanner` never built directly; public `GetPlanOf<THandler>()` and `GetPlanOf(Type)` (research R-008) returning the handler's builder, created on first call with its `[DependsOn]`/`[DependentRun]` preloaded, `ThrowIfLocked`; at `Configure` a `GetPlanOf` for a type that isn't a registered recurring handler throws naming it (R-007); protected virtuals `IsRecurringHandlerEnabledByDefault()` and `DefaultRecurringTimeZone` (R-009) replacing `IsRecurringHandlerEnabled` (depends on T022)
- [X] T027 [US2] `ResolvePlan` in `Src/ExecutionFlow/ExecutionFlowOptions.cs` following plan.md "Internal planner and options layer": (1) user plan → one trace warning per handler in it with `GetPlanOf` values (R-006), merge the user step's schedule/Id/name, re-resolve with `ConfiguredEnabled ?? global`; (2) no user plan and dependencies/run modes in attributes or `GetPlanOf` → automatic plan from every registered handler using the `GetPlanOf` builders, `Build(AutomaticPlanMissingPrerequisite)`, re-resolve; (3) otherwise merge each `GetPlanOf` builder's schedule/Id/name into the registration. Add internal `IsEnabled(type)` and `ResolveTimeZoneId(type)` (plan step → `GetPlanOf` → attribute → global; intervals → none) (FR-006, R-003; depends on T023)
- [X] T024 [US2] Hangfire in `Src/ExecutionFlow.Hangfire/HangfireOptions.cs`, `HangfireSetup.cs` and `Infrastructure/RecurringJobResolver.cs`: `SetJobAutoRun`/`SetJobTimeZone` (both overloads) `[Obsolete("Use GetPlanOf<T>().Enabled(...)")]` / `[Obsolete("Use GetPlanOf<T>().TimeZone(...)")]` calling `GetPlanOf`; override `IsRecurringHandlerEnabledByDefault()` with `GlobalRecurringAutoRun` and `DefaultRecurringTimeZone` with `RecurringTimeZone`; `RecurringJobResolver.IsAutoRun`/`ResolveTimeZoneId` call the core resolution; remove the internal `RecurringAutoRun`/`RecurringTimeZones` and the old warnings superseded by R-006; keep the interval + time-zone warning (012) on the effective time zone (FR-006; depends on T027)
- [X] T028 [US2] Move every internal use of the obsolete methods (tests, including `Tests/ExecutionFlow.Hangfire.Tests/IntervalRegistrationTests.cs` for the 012 interval + time-zone warning, `Examples/`, README) to `GetPlanOf`, keeping only the tests that assert the obsolete shortcuts; the build has obsolete warnings only there; README section "Handler configuration" explains the internal planner vs the user planner and `GetPlanOf<T>()`; one-line notes under 005 FR-003/FR-008 (FR-006; depends on T024)
- [X] T025 Update `specs/011-local-hosting` (spec FR-002, AC-002.2..AC-002.4, AC-003.4, plan, tasks T003, whose `IsRecurringHandlerEnabled` override becomes the R-009 virtuals): no `SetJobAutoRun<T>`/`SetJobTimeZone<T>` in `HostingOptions`, only `GetPlanOf<T>()` and the global options (decided 2026-11-02); `HostingOptions` overrides the R-009 virtuals
- [X] T029 Run `quickstart.md` scenarios 1-7, the full suite, and `/speckit-analyze` (depends on T026..T028, T025)

## Phase 7: Polish & Cross-Cutting

- [X] T016 [P] README: configuration classes (inline and class), the precedence table per item, the warnings, Hangfire extensions; the `[Timeout]` example is already removed (R-005)
- [X] T017 [P] Amendment notes: one-line notes under 005 FR-003, FR-008, FR-009; 010 FR-001; 012 FR-004; 013 FR-002, pointing to 014; and update `specs/013-dependencies-by-attribute/tasks.md` so 013 builds on `HandlerBuilder` (drop its enum/`RunMode`/migration tasks already done here)
- [X] T018 Run `quickstart.md` scenarios 1-6 and `/speckit-analyze`

## Dependencies & Execution Order

- Done: T001..T021 (FR-001..FR-005, `HandlerBuilder` abstract).
- FR-006: T026 (spec text) and T022 (tests) → T023 → T027 → T024 → T028 → T025 → T029.

## Parallel Opportunities

- T026 (spec text) and T022 (tests) in parallel.
- T025 (011 spec) can be written while T023..T028 are implemented.

## Implementation Strategy

1. Done: FR-001..FR-005.
2. FR-006: spec text and tests first (T026, T022), then the core internal planner and resolution (T023, T027), then Hangfire and the obsolete shortcuts (T024), then callers and docs (T028).
3. Align 011 (T025) and validate (T029).
