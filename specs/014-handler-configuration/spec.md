# Feature Specification: Handler configuration classes (EF Core style)

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-11-02

**Status**: Implemented (v1.3.0, unreleased)

**Input**: User description: "EF Core-style configuration classes for recurring handlers." Decisions taken with the maintainer on 2026-11-02 (see Clarifications).

**Packages**: `ExecutionFlow`, `ExecutionFlow.Hangfire`

**Amends**:
- [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-003 and FR-008 (`SetJobAutoRun<T>`/`SetJobTimeZone<T>` become `[Obsolete]` shortcuts to `GetPlanOf<T>()`, the options layer of the internal planner), FR-009 (a handler of the user plan drops that layer as a whole with a warning instead of throwing).
- [010-execution-plan](../010-execution-plan/spec.md) FR-001 (`Add<T>()` returns `HandlerBuilder<T>`, which replaces `ExecutionPlanEntry`; the planner applies configuration classes; a handler configured twice throws).
- [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-004 (same change as 005 FR-009).
- [013-dependencies-by-attribute](../013-dependencies-by-attribute/spec.md) FR-002 (the builder also sets the run mode).

## Context

Configuring a recurring handler is spread today over attributes on the class (`[Recurring]`, `[RecurringInterval]`, `[DisplayName]`, `[NoOverlap]`, `[DependsOn]`, `[DependentRun]`), per-handler options (`SetJobTimeZone<T>`, `SetJobAutoRun<T>`) and the plan entry. As handlers grow, consumers want one place per handler, like EF Core's `IEntityTypeConfiguration<T>`, where everything about that handler is configured, and where explicit configuration overrides attributes item by item, exactly as EF's fluent API overrides data annotations.

## Clarifications

### Session 2026-11-02

- Q: Shape of the configuration class? → A: `IHandlerConfiguration<THandler>` with `void Configure(HandlerBuilder<THandler> handler)`, like `IEntityTypeConfiguration<T>`.
- Q: What does the builder configure? → A: Everything provider-neutral: `Cron(...)`/`Interval(...)` (one replaces the other), `Id(...)`, `TimeZone(...)`, `DisplayName(...)`, `Enabled(bool)`, `DependsOn<X>()`, `RemoveDependency<X>()`, `ClearDependencies()`, `RunMode(DependentRunMode, TimeSpan?)`, `NoOverlap(TimeSpan)`, `AllowOverlap()`.
- Q: Provider-specific settings? → A: Extension methods from the provider package, e.g. `handler.Queue("reports")` in `ExecutionFlow.Hangfire` (queue and retries).
- Q: A timeout extension? → A: No. Hangfire 1.8 has no execution-timeout filter (`LatencyTimeoutAttribute` only deletes jobs waiting in the queue); the README's `[Timeout]` example, which refers to a non-existent attribute, is removed (maintainer, 2026-11-02).
- Q: Where are configuration classes applied? → A: Explicitly on the planner: `planner.ApplyConfigurationsFromAssembly(assembly)` and `planner.ApplyConfiguration(new XConfiguration())`, together with today's inline `planner.Add<T>()`.
- Q: What does inline `planner.Add<T>()` return? → A: The same `HandlerBuilder<T>` as a configuration class; `ExecutionPlanner` becomes the single orchestrator of every handler's configuration. `HandlerBuilder<T>` replaces `ExecutionPlanEntry` (removed): a public API change of the unreleased 1.3.0, confirmed by the maintainer (constitution V; nothing published, no major bump). A handler without dependencies can be in the planner just to be configured; it is an independent step of the plan.
- Q: How does a configuration class say which provider (Hangfire or the local host) it belongs to? → A: It doesn't (option A). Each provider gets its own plan, built from the configuration classes chosen by whoever builds it, filtered by assembly or namespace with `ApplyConfigurationsFromAssembly(assembly, predicate)`; the automatic plan likewise follows each provider's `Scan(assembly, predicate)`. The filter is the consumer's responsibility (maintainer, 2026-11-02).
- Q: A shortcut on the options? → A: Yes: `ExecutionFlowOptions.UsePlanFromAssembly(assembly, predicate = null, policy = MissingPrerequisite.Throw)` builds a planner from the assembly's configuration classes and registers the plan; hiding the planner is fine when only loading configurations. The explicit planner stays the main documented way (maintainer, 2026-11-02).
- Q: How are per-handler options configured? → A: Through the provider's **internal planner**: `options.GetPlanOf<THandler>()` returns the `HandlerBuilder<THandler>` of the options. The internal planner holds what the library reads today (the class's attributes) plus `GetPlanOf<T>()`, which wins over the attributes; without a user plan it is what forms the automatic plan (013). `SetJobAutoRun<T>`/`SetJobAutoRun(Type, bool)` and `SetJobTimeZone<T>`/`SetJobTimeZone(Type, string)` become `[Obsolete]` shortcuts for `GetPlanOf<T>().Enabled(...)` / `.TimeZone(...)`, so nothing published breaks (`SetJobAutoRun` exists in v1.0.2). The local host has no per-handler option methods: only `GetPlanOf<T>()` (maintainer, 2026-11-02).
- Q: A handler in both the internal planner and the user's planner (configuration class, `Add<T>()`, `UsePlan`, `UsePlanFromAssembly`)? → A: The user's planner replaces the internal planner's configuration of that handler **as a whole**: `GetPlanOf<T>()` (and the obsolete options) stop counting for it, with a trace warning when they had values. The class's attributes still apply item by item under the user's builder, as in EF (reading A). This deprecates AC-002.6 and AC-002.7, which let an option apply to a handler of the user's plan (maintainer, 2026-11-02).
- Q: Can `HandlerBuilder` be created or extended outside the library? → A: No: the non-generic `HandlerBuilder` is abstract with an internal constructor; only `HandlerBuilder<THandler>` exists, created by the planners (maintainer, 2026-11-02).
- Q: Warnings for the options layer (research R-006)? → A: One trace warning per handler of the user plan that had `GetPlanOf` values, whatever items the user plan sets; it replaces the per-item warning (maintainer, 2026-11-02).
- Q: `GetPlanOf<T>()` for a handler that isn't registered (R-007)? → A: It doesn't register it; `Configure` throws naming it, as `SetJobAutoRun` does today. Registering stays with `Scan`/`Add` (maintainer, 2026-11-02).
- Q: A `Type` overload (R-008)? → A: Yes, public `GetPlanOf(Type)` besides `GetPlanOf<T>()`, like `Add(Type)`; the obsolete `Type` overloads in Hangfire call it (maintainer, 2026-11-02).
- Q: Where do the global defaults live (R-009)? → A: They stay on each provider's options (`GlobalRecurringAutoRun`, `RecurringTimeZone`); the core resolves each handler and asks the provider for its defaults (maintainer, 2026-11-02).
- Q: Same handler configured twice? → A: Error (two classes, or a class plus `Add<T>()`).
- Q: Precedence? → A: Per item, resolved independently; only what the builder calls changes. Global default < attribute < per-handler option < builder (EF: convention < data annotation < fluent API).
- Q: Builder vs per-handler option? → A: The builder wins, and configuration emits a trace warning naming the handler and the ignored option. This replaces today's error for `SetJobAutoRun` + plan (005 FR-009, 012 FR-004): a change of documented behavior of the unreleased 1.3.0, confirmed by the maintainer (constitution V; nothing published, no major bump).
- Q: Dependencies? → A: Additive: attribute and builder merge; `RemoveDependency<X>()` removes one, `ClearDependencies()` removes all, including the attribute's.
- Q: Overlap? → A: `AllowOverlap()` turns overlap back on when `[NoOverlap]` turned it off.
- Q: Time zone? → A: `TimeZone(null)`, empty or unknown throws. On an interval handler, a time zone from the builder or `SetJobTimeZone<T>` is ignored with a trace warning; the global `RecurringTimeZone` is ignored silently.
- Q: Attributes of 013? → A: Keep working; configuration classes win over them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure a handler in a dedicated class (Priority: P1)

As a consumer app, I want one configuration class per handler where I set its schedule, ID, name, dependencies and run behavior, so that each handler's configuration lives in one place.

**Why this priority**: it is the feature.

**Independent Test**: a `ProductSyncConfiguration` sets `Cron`, `Id`, `DependsOn` and `NoOverlap`; after `ApplyConfiguration` and `Build`, the step and the Hangfire registration reflect every value.

**Acceptance Scenarios**: AC-001.1..AC-001.6.

---

### User Story 2 - Override attributes item by item (Priority: P1)

As a handler author, I want attributes on the class to keep working and the configuration class to change only what it sets, so that I can move configuration gradually.

**Why this priority**: without it, adopting configuration classes would mean rewriting every attribute.

**Independent Test**: a class with `[Recurring(cron, Id)]` and `[NoOverlap(300)]` and a configuration that only calls `NoOverlap(10 min)` keeps the attribute's cron and Id and uses 10 minutes.

**Acceptance Scenarios**: AC-002.1..AC-002.5, AC-002.8..AC-002.11, AC-006.1..AC-006.7.

---

### User Story 3 - Apply all configurations at once (Priority: P2)

As a consumer app, I want to apply every configuration class of an assembly with one call, as EF's `ApplyConfigurationsFromAssembly`.

**Why this priority**: convenience; explicit `ApplyConfiguration` already works.

**Independent Test**: an assembly with three configuration classes applied with one call yields three configured steps.

**Acceptance Scenarios**: AC-003.1..AC-003.3, AC-005.1..AC-005.3.

---

### User Story 4 - Provider-specific settings in the same class (Priority: P3)

As a consumer app on Hangfire, I want to set the queue and retries in the configuration class too.

**Why this priority**: Hangfire attributes already cover it; this keeps everything in one place.

**Independent Test**: `handler.Queue("reports")` in a configuration makes the recurring job use the `reports` queue.

**Acceptance Scenarios**: AC-004.1, AC-004.2.

### Edge Cases

- A configuration class for a handler that isn't a recurring handler: applying it throws naming the type.
- `Cron(...)` on a handler with `[RecurringInterval]`: the cron replaces the interval (and the reverse).
- `RemoveDependency<X>()` for a dependency that doesn't exist: no effect.
- `ClearDependencies()` then `DependsOn<Y>()`: only `Y` remains.
- `AllowOverlap()` on a dependent of the plan: dependents never overlap anyway (005 FR-012); no effect.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Configuration class and builder**: `IHandlerConfiguration<THandler>` (where `THandler` is a recurring handler) has `Configure(HandlerBuilder<THandler>)`. The builder exposes the provider-neutral settings listed in Clarifications; each call records an explicit value for that item. Inline `planner.Add<T>()` returns the same `HandlerBuilder<T>`, so inline and class configuration have identical capabilities; `HandlerBuilder<T>` replaces `ExecutionPlanEntry`, and `ExecutionPlanner` is the single orchestrator of every handler's configuration. *(Story 1)*
  - **AC-001.1**: **Given** a configuration that calls `Cron`, `Id`, `DisplayName`, `DependsOn`, `RunMode` and `NoOverlap`, **When** it is applied and the plan is built, **Then** the step and the registration expose every value. Tests: `HandlerConfigurationTests.Apply_SetsEveryValue` (step), `HandlerConfigurationHangfireTests.Build_RegistersBuilderValues` (registration)
  - **AC-001.2**: **Given** `Interval(90 min)` on a handler with `[Recurring(cron)]`, **When** applied, **Then** the handler runs on the interval and has no cron. Test: `HandlerConfigurationTests.Interval_ReplacesAttributeCron`
  - **AC-001.3**: **Given** `TimeZone(null)`, `TimeZone("")` or an unknown time zone, **When** configured, **Then** it throws naming the handler. Tests: `HandlerConfigurationTests.TimeZone_Throws_WhenNullOrEmpty`, `HandlerConfigurationHangfireTests.TimeZone_Throws_WhenUnknown` (the provider validates time zone IDs)
  - **AC-001.4**: **Given** a configuration for a type that isn't a recurring handler, **When** applied, **Then** it throws naming the type. Test: `HandlerConfigurationTests.Apply_Throws_WhenTypeIsNotRecurringHandler`
  - **AC-001.5**: **Given** a handler configured by two classes, or by a class and `Add<T>()`, **When** the second is applied, **Then** it throws naming the handler. Test: `HandlerConfigurationTests.Apply_Throws_WhenHandlerConfiguredTwice`
  - **AC-001.6**: **Given** `planner.Add<T>()`, **When** it returns, **Then** it is a `HandlerBuilder<T>` with the same methods as in a configuration class (e.g. `.Cron(...)` inline). Test: `HandlerConfigurationTests.InlineAdd_ReturnsHandlerBuilder`

- **FR-002 Precedence per item**: each item is resolved independently, from lowest to highest: global default < attribute < per-handler option < builder. *(Amended by FR-006: the per-handler option is the internal planner's `GetPlanOf<T>()`, and for a handler in the user's plan it doesn't count at all.)* Only items the builder sets change. Dependencies merge (attribute + builder), with `RemoveDependency<X>()` and `ClearDependencies()` as the only way to drop attribute dependencies. When the builder overrides a per-handler option (`SetJobAutoRun<T>`, `SetJobTimeZone<T>`), configuration emits a trace warning naming the handler and the option, and no longer throws. *(Story 2)*
  Precedence table (maintainer, 2026-11-02), lowest to highest, per item. The "per-handler option" column is the internal planner's `GetPlanOf<T>()`; for a handler in the user plan it doesn't count at all (FR-006):

  | Item | Default | Global option | Attribute | Per-handler option | Builder |
  |---|---|---|---|---|---|
  | Schedule | none | — | `[Recurring]` / `[RecurringInterval]` | — | `Cron` / `Interval` |
  | Id | `IJobIdGenerator` | — | `Id = ...` | — | `Id` |
  | Time zone | UTC | `RecurringTimeZone` | `TimeZone = ...` | `GetPlanOf<T>().TimeZone` (obsolete `SetJobTimeZone<T>`) | `TimeZone` |
  | Display name | class name | — | `[DisplayName]` | — | `DisplayName` |
  | Enabled | on | `GlobalRecurringAutoRun` | — | `GetPlanOf<T>().Enabled` (obsolete `SetJobAutoRun<T>`) | `Enabled` |
  | Dependencies | none | — | `[DependsOn]` (merged) | — | `DependsOn` / `RemoveDependency` / `ClearDependencies` |
  | Run mode | triggered | — | `[DependentRun]` | — | `RunMode` |
  | Overlap | allowed | — | `[NoOverlap]` | — | `NoOverlap` / `AllowOverlap` |
  | Queue, retries (Hangfire) | Hangfire defaults, `DisableRecurringRetries` | — | `[Queue]`, `[AutomaticRetry]` | — | `Queue`, `AutomaticRetry` |

  - **AC-002.1**: **Given** `[Recurring("*/10 * * * *", Id = "a")]`, `[NoOverlap(300)]` and a configuration that only calls `NoOverlap(10 min)`, **When** applied, **Then** the cron and Id come from the attribute and the overlap timeout is 10 minutes. Test: `HandlerConfigurationTests.Builder_ChangesOnlyWhatItSets`
  - **AC-002.2**: **Given** `[DependsOn(typeof(A))]` and `DependsOn<B>()` in the builder, **When** built, **Then** the prerequisites are `A` and `B`. Test: `HandlerConfigurationTests.Dependencies_Merge`
  - **AC-002.3**: **Given** `[DependsOn(typeof(A))]` and `RemoveDependency<A>()`, or `ClearDependencies()`, **When** built, **Then** `A` isn't a prerequisite. Tests: `HandlerConfigurationTests.RemoveDependency_DropsAttributeDependency`, `HandlerConfigurationTests.ClearDependencies_DropsAll`
  - **AC-002.4**: **Given** `[NoOverlap(300)]` and `AllowOverlap()`, **When** built, **Then** overlap is allowed. Test: `HandlerConfigurationTests.AllowOverlap_TurnsOverlapBackOn`
  - **AC-002.5**: **Given** `GetPlanOf<T>().Enabled(false)` (or the obsolete `SetJobAutoRun<T>(false)`) and the user plan's builder with `Enabled(true)` for `T`, **When** configured, **Then** the user plan's value is used, one trace warning names `T` and the ignored options layer (research R-006), and nothing throws. Test: `HandlerConfigurationHangfireTests.Builder_WinsOverPerHandlerOption_WithWarning`
  - ~~**AC-002.6**~~ (deprecated 2026-11-02 by FR-006: a handler in the user's plan ignores the options layer as a whole.)
  - ~~**AC-002.7**~~ (deprecated 2026-11-02 by FR-006: same reason; an option turning off a handler only counts outside the user's plan.)
  - **AC-002.8**: **Given** `[Recurring(cron, Id = "a", TimeZone = "Europe/Lisbon")]` and `[DisplayName("Attr")]`, and a builder that sets `Id("b")`, `TimeZone("America/Sao_Paulo")` and `DisplayName("Builder")`, **When** built and registered, **Then** the builder's Id, time zone and name are used. Test: `HandlerConfigurationHangfireTests.Builder_WinsOverAttribute_ForIdTimeZoneAndName`
  - **AC-002.9**: **Given** `RemoveDependency<X>()` for a prerequisite that was never declared, **When** built, **Then** nothing changes and nothing throws. Test: `HandlerConfigurationTests.RemoveDependency_IsNoOp_WhenNotDeclared`
  - **AC-002.10**: **Given** `[DependsOn(typeof(A))]`, `ClearDependencies()` and then `DependsOn<B>()`, **When** built, **Then** only `B` is a prerequisite. Test: `HandlerConfigurationTests.ClearThenDependsOn_KeepsOnlyNewDependency`
  - **AC-002.11**: **Given** `AllowOverlap()` on a dependent triggered by the plan, **When** a prerequisite completes while the dependent runs, **Then** the dependent still isn't triggered again until it finishes (005 FR-012). Test: `HandlerConfigurationHangfireTests.AllowOverlap_DoesNotLetDependentOverlap`

- **FR-003 Applying configurations**: `planner.ApplyConfiguration(IHandlerConfiguration<T>)` applies one instance; `planner.ApplyConfigurationsFromAssembly(assembly)` creates and applies every non-abstract class in the assembly that implements `IHandlerConfiguration<T>` and has a parameterless constructor. An overload `ApplyConfigurationsFromAssembly(assembly, predicate)` filters the configuration classes, as EF Core's does and as `Scan(assembly, predicate)` (added 2026-11-02 during implementation; additive). *(Story 3)*
  - **AC-003.1**: **Given** an assembly with three configuration classes, **When** `ApplyConfigurationsFromAssembly` runs, **Then** three handlers are configured. Test: `HandlerConfigurationTests.ApplyFromAssembly_AppliesEveryConfiguration`
  - **AC-003.2**: **Given** an abstract configuration class or one without a parameterless constructor, **When** scanning, **Then** abstract classes are skipped and the other case throws naming the class. Test: `HandlerConfigurationTests.ApplyFromAssembly_SkipsAbstract_ThrowsWithoutParameterlessConstructor`
  - **AC-003.3**: **Given** classes applied from an assembly and an inline `Add<T>()` for another handler, **When** built, **Then** both are in the plan. Test: `HandlerConfigurationTests.ApplyFromAssembly_CoexistsWithInlineAdd`

- **FR-004 Provider extensions (Hangfire)**: the core builder exposes a public `HasSetting(key, value)` for provider packages (research R-004). `ExecutionFlow.Hangfire` adds builder extensions for the queue and automatic retries. They win over the equivalent Hangfire attributes on the handler. *(Story 4)*
  - **AC-004.1**: **Given** `handler.Queue("reports")`, **When** the recurring job is registered, **Then** it uses the `reports` queue, even if the class has another `[Queue]`. Test: `HandlerConfigurationHangfireTests.Queue_WinsOverAttribute`
  - **AC-004.2**: **Given** a retry setting from the builder, **When** the job fails, **Then** the builder's retry rule applies instead of the attribute's or `DisableRecurringRetries`. Test: `HandlerConfigurationHangfireTests.Retry_WinsOverAttributeAndOption`
  - ~~**AC-004.3**~~ (deprecated 2026-11-02: no timeout extension, research R-005.)

- **FR-005 Plan from an assembly (shortcut)**: `ExecutionFlowOptions.UsePlanFromAssembly(Assembly assembly, Func<Type, bool> predicate = null, MissingPrerequisite policy = MissingPrerequisite.Throw)` is `UsePlan(new ExecutionPlanner().ApplyConfigurationsFromAssembly(assembly, predicate).Build(policy))`, on every provider's options. Calling it together with `UsePlan` (or twice) throws, like two `UsePlan` calls. *(Story 3)*
  - **AC-005.1**: **Given** an assembly with configuration classes in two namespaces, **When** `UsePlanFromAssembly(assembly, t => t.Namespace == "A")` is called, **Then** the plan has only namespace `A`'s handlers, configured by their classes. Test: `HandlerConfigurationTests.UsePlanFromAssembly_AppliesFilteredConfigurations`
  - **AC-005.2**: **Given** `UsePlanFromAssembly` after `UsePlan`, **When** it is called, **Then** it throws. Test: `HandlerConfigurationTests.UsePlanFromAssembly_Throws_WhenPlanAlreadyRegistered`
  - **AC-005.3**: **Given** `policy = SkipDependents` and a configuration whose prerequisite isn't configured, **When** called, **Then** the dependent is turned off instead of throwing. Test: `HandlerConfigurationTests.UsePlanFromAssembly_UsesPolicy`

- **FR-006 Internal planner and the options layer**: each provider's options have an internal planner made of the class's attributes plus `options.GetPlanOf<THandler>()` (a `HandlerBuilder<THandler>`, the same builder as everywhere else), with `GetPlanOf` winning over attributes per item. Without a user plan, its values apply to the handler, and it forms the automatic plan when it has dependencies or run modes (013). When the handler is in the user's planner, the user's configuration replaces the internal planner's `GetPlanOf` values as a whole (attributes still apply per item), and configuration writes a trace warning naming the handler when `GetPlanOf` had values. `SetJobAutoRun`/`SetJobTimeZone` (both overloads) are `[Obsolete]` and call `GetPlanOf`. `HandlerBuilder` is abstract with an internal constructor. *(Stories 1, 2)*
  - **AC-006.1**: **Given** `GetPlanOf<T>().Enabled(false).TimeZone("America/Sao_Paulo")` and no user plan, **When** configured, **Then** `T` doesn't auto-run and uses that time zone. Test: `HandlerConfigurationHangfireTests.GetPlanOf_AppliesWithoutUserPlan`
  - **AC-006.2**: **Given** `GetPlanOf<T>().Enabled(false)` and `T` in the user's plan without `Enabled`, **When** configured, **Then** `T` auto-runs (the options layer is replaced as a whole) and a trace warning names `T`. Test: `HandlerConfigurationHangfireTests.UserPlan_ReplacesGetPlanOf_AsAWhole`
  - **AC-006.3**: **Given** `[Recurring(cron, Id)]` on `T` and `T` in the user's plan with only `DependsOn`, **When** configured, **Then** the cron and Id come from the attribute. Test: `HandlerConfigurationHangfireTests.UserPlan_KeepsAttributes_PerItem`
  - **AC-006.4**: **Given** the obsolete `SetJobAutoRun<T>(false)` / `SetJobTimeZone<T>(...)`, **When** configured without a user plan, **Then** they behave as `GetPlanOf<T>().Enabled(false)` / `.TimeZone(...)` and compile with an obsolete warning. Test: `HandlerConfigurationHangfireTests.ObsoleteOptions_RouteToGetPlanOf`
  - **AC-006.5**: **Given** `GetPlanOf<B>().DependsOn<A>()` and no user plan, **When** configured, **Then** the automatic plan is formed with `A` as `B`'s prerequisite. Test: `AutomaticPlanTests.GetPlanOf_DependsOn_FormsAutomaticPlan`
  - **AC-006.7**: **Given** `GetPlanOf<T>()` for a type that isn't a registered recurring handler, **When** the options are configured, **Then** it throws naming the type (research R-007). Test: `HandlerConfigurationTests.GetPlanOf_Throws_WhenHandlerNotRegistered`
  - **AC-006.6**: **Given** code outside the library, **When** it tries to create or derive from `HandlerBuilder`, **Then** it can't (abstract, internal constructor). Test: `HandlerConfigurationTests.HandlerBuilder_IsAbstract_WithNoPublicConstructor`

### Business Rules

- **RN-001**: The builder never changes an item it doesn't call (EF semantics).
- **RN-002**: Provider extensions only exist in provider packages; the core builder stays dependency-free (constitution I).
- **RN-003**: The warnings of FR-002 use the trace channel, like the other configuration warnings (012 AC-002.6).

### Key Entities

- **`IHandlerConfiguration<THandler>`**: one configuration class per handler.
- **`HandlerBuilder<THandler>`**: records explicit values per item.
- **Item**: an independently resolved setting (schedule, Id, time zone, name, enabled, dependencies, run mode, overlap, provider settings).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every item of the precedence table in FR-002 has at least one test showing the builder overriding the attribute or option: schedule (AC-001.2), Id, time zone and name (AC-002.8), enabled and time zone vs options (AC-002.5), dependencies (AC-002.2, AC-002.3, AC-002.10), overlap (AC-002.1, AC-002.4), run mode (013 AC-004.3), Hangfire queue and retries (AC-004.1, AC-004.2).
- **SC-002**: A handler can be fully configured, including dependencies and the Hangfire queue, without any attribute on the class (AC-001.1, AC-004.1).
- **SC-003**: Existing plan and recurring tests pass, except the three tests of the deprecated AC-002.6/AC-002.7 (`PerHandlerOption_Applies_WhenBuilderDoesNotSetItem`, `OptionTurningOffPrerequisite_FollowsPlanPolicy`, the AC-002.6 test of `ExecutionPlanHangfireTests`), which become FR-006 tests (the last one is now `ExecutionPlanHangfireTests.GetPlanOf_IsIgnored_ForUserPlanHandler`).

## Assumptions

- Job names (maintainer, 2026-11-02): the plan's `DisplayName(...)` only names recurring jobs. Event (queue) handlers can't be in a plan, so an event job's non-empty `ICustomNameEvent.CustomName` is never overridden; only a null or empty one falls back to the attribute chain of 008.
- Configuration classes are created with their parameterless constructor (as EF); no DI inside configuration classes.
- Out of scope: configuration classes for event handlers; local-host-specific extensions (011 can add them later).
- Documentation requirement: the README shows the precedence table and a configuration class example.
