# Feature Specification: Dependencies by attribute (automatic execution plan)

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Dependencies between recurring handlers declared by attribute, without an explicit execution plan." Decisions taken with the maintainer on 2026-11-02 (see Clarifications).

**Packages**: `ExecutionFlow`, `ExecutionFlow.Hangfire` (and `ExecutionFlow.Hosting` through [011](../011-local-hosting/spec.md))

**Amends**:
- [010-execution-plan](../010-execution-plan/spec.md) FR-007 (the entry's run mode becomes a single `RunMode(DependentRunMode, ...)` method replacing `MinInterval()` and `RunOnOwnSchedule()`; the planner also reads `[DependentRun]`, and the entry wins).
- [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-009 (without `UsePlan`, Hangfire applies the automatic plan).
- [011-local-hosting](../011-local-hosting/spec.md) FR-005 (the local host applies the same automatic plan).

## Context

`[DependsOn]` already exists (010 FR-002), but it only takes effect inside an `ExecutionPlanner`. A handler registered with `Scan` or `Add` that carries `[DependsOn(typeof(X))]` and no explicit plan has its dependency silently ignored today: it runs on its own schedule as if the dependency didn't exist.

This feature lets a consumer declare dependencies and run modes entirely with attributes. Without an explicit plan, the registered handlers form an **automatic plan** with the same rules and validation as an explicit one, so simple setups need no planner code.

## Clarifications

### Session 2026-11-02

- Q: How is the run mode declared without a plan? → A: A separate `[DependentRun]` attribute, at most one per class, on the dependent handler. `[DependsOn]` stays repeatable and only carries the prerequisite type.
- Q: How are the modes expressed? → A: One mode at a time, the same way in the attribute and in the plan: `DependentRunMode.MinInterval` (with an interval) or `DependentRunMode.OwnSchedule`. In the plan, `ExecutionPlanEntry.RunMode(...)` **replaces** `MinInterval()` and `RunOnOwnSchedule()` of 010. Confirmed by the maintainer as a removal of public API of the unreleased 1.3.0 (constitution V; no major bump, nothing published) (2026-11-02).
- Q: Attribute and plan entry with different modes? → A: The plan wins, as for every other item (attribute < builder).
- Q: Minimum `MinInterval`? → A: Any positive value, as the plan entry (no one-minute minimum).
- Q: What forms the automatic plan? → A: Without `UsePlan`, the recurring handlers registered with `Scan`/`Add`; enabled/disabled comes from the existing `SetJobAutoRun`/`GlobalRecurringAutoRun`.
- Q: Missing or disabled prerequisite in the automatic plan? → A: A global option on the options; default `Throw`, alternative `SkipDependents`, with the semantics of `MissingPrerequisite`.
- Q: Where does the automatic plan's missing-prerequisite option live? → A: `ExecutionFlowOptions.AutomaticPlanMissingPrerequisite`, in the core, shared by every provider.
- Q: With an explicit `UsePlan`, handlers outside the plan that have `[DependsOn]`? → A: Ignored silently, as today; documented only.
- Q: With an explicit `UsePlan`, handlers in the plan that have `[DependentRun]`? → A: The planner reads it; the entry's `MinInterval`/`RunOnOwnSchedule` win over the attribute.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare dependencies with attributes only (Priority: P1)

As a handler author, I want to put `[DependsOn]` on a recurring handler and have it respected without writing a plan, so that a simple chain of jobs needs no configuration code.

**Why this priority**: it removes the silent trap where `[DependsOn]` is ignored, and it is the core of the feature.

**Independent Test**: register `A` and `B` with `[DependsOn(typeof(A))]` through `Scan`, build Hangfire without `UsePlan`, and check that `B` is triggered by `A` instead of running on its own schedule.

**Acceptance Scenarios**: AC-001.1..AC-001.5.

---

### User Story 2 - One run mode, in the attribute and in the plan (Priority: P1)

As a handler author, I want to say on the class that a dependent waits a minimum interval or keeps its own schedule, so that the run mode lives next to the handler.

**Why this priority**: without it, any dependent beyond the default mode would still need a planner.

**Independent Test**: put `[DependentRun(DependentRunMode.MinInterval, "00:20:00")]` on a dependent and check the step exposes a 20-minute minimum interval.

**Acceptance Scenarios**: AC-002.1..AC-002.5.

---

### User Story 3 - Invalid dependencies fail at startup (Priority: P1)

As a consumer app, I want a cycle or a missing prerequisite among the attributes to fail at configuration with a clear message, as an explicit plan does.

**Why this priority**: a silently broken chain is the failure the execution plan exists to prevent.

**Independent Test**: register two handlers that depend on each other by attribute and check the configuration throws with the cycle path.

**Acceptance Scenarios**: AC-003.1..AC-003.4.

---

### User Story 4 - Explicit plans keep working (Priority: P2)

As a consumer app that already uses `UsePlan`, I want the explicit plan to stay in charge, with `[DependentRun]` usable on its handlers and the entry winning over it.

**Why this priority**: compatibility with the explicit plan of 010; no new capability.

**Independent Test**: build an explicit plan where a handler has `[DependentRun(MinInterval = ...)]` and the entry sets another value, and check the entry's value is used.

**Acceptance Scenarios**: AC-004.1..AC-004.3.

### Edge Cases

- No registered handler has `[DependsOn]` or `[DependentRun]` and there is no `UsePlan`: no automatic plan is formed; behavior is exactly as before this feature.
- `[DependentRun]` without `[DependsOn]` and no `UsePlan`: the automatic plan is formed, and it fails (next case), so the attribute is never silently ignored.
- `[DependentRun]` on a handler without any prerequisite: configuration fails, as for a plan entry (010 AC-007.4).
- A prerequisite that is registered but turned off with `SetJobAutoRun(false)`: it counts as unavailable (default: configuration fails; with `SkipDependents`, the dependents are turned off in cascade).
- A handler with `[DependsOn]` whose prerequisite isn't a registered recurring handler: unavailable, same rule as above.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Automatic plan**: when the options have no explicit plan (`UsePlan` not called) and at least one registered recurring handler has `[DependsOn]` or `[DependentRun]`, the registered recurring handlers form an automatic plan, in registration order, ordered and validated exactly like an explicit plan (010 FR-004..FR-006). Each handler's enabled state comes from `SetJobAutoRun<T>`, else `GlobalRecurringAutoRun`. Providers apply the automatic plan exactly as they apply an explicit one (005 FR-009..FR-012; 011 FR-005). *(Story 1)*
  - **AC-001.1**: **Given** `A` and `B` with `[DependsOn(typeof(A))]` registered by `Scan`, and no `UsePlan`, **When** the options are configured, **Then** a plan exists where `A` is a prerequisite of `B`. Test: `AutomaticPlanTests.Configure_BuildsAutomaticPlan_FromDependsOnAttributes`
  - **AC-001.2**: **Given** the automatic plan in Hangfire, **When** it is built, **Then** `B` is registered as triggered by its prerequisite (`Cron.Never()`), like a dependent of an explicit plan. Test: `AutomaticPlanHangfireTests.Build_RegistersDependent_AsTriggeredByPlan`
  - **AC-001.3**: **Given** no registered handler has `[DependsOn]` or `[DependentRun]` and no `UsePlan`, **When** the options are configured, **Then** no plan exists and registration is unchanged. Test: `AutomaticPlanTests.Configure_FormsNoPlan_WhenNoHandlerHasDependsOn`
  - **AC-001.4**: **Given** `SetJobAutoRun<A>(false)` in the automatic plan, **When** the plan is read, **Then** `A`'s step is disabled, and `SetJobAutoRun` doesn't throw (unlike with an explicit plan, 005 FR-009). Test: `AutomaticPlanHangfireTests.Configure_TakesEnabledFromSetJobAutoRun`
  - **AC-001.5**: **Given** a registered handler with `[DependentRun]` but no `[DependsOn]`, and no `UsePlan`, **When** the options are configured, **Then** the automatic plan is formed and fails as AC-002.5, so the attribute is never silently ignored. Test: `AutomaticPlanTests.Configure_FormsPlan_WhenOnlyDependentRunIsPresent`

- **FR-002 One run mode, the same in the attribute and the plan**: a dependent declares at most one run mode: `[DependentRun(DependentRunMode.MinInterval, "hh:mm:ss")]` (same format as `[RecurringInterval]`, 012 FR-001, any positive value) or `[DependentRun(DependentRunMode.OwnSchedule)]`, at most once per class; and in the plan `entry.RunMode(DependentRunMode.MinInterval, TimeSpan)` or `entry.RunMode(DependentRunMode.OwnSchedule)`, which replaces 010's `MinInterval()`/`RunOnOwnSchedule()`. The modes mean exactly what 010 FR-007 defines. *(Story 2)*
  - *Amended by [014-handler-configuration](../014-handler-configuration/spec.md) FR-001: `RunMode` lives on `HandlerBuilder<T>` (014 replaced `ExecutionPlanEntry`); `DependentRunMode` already exists.*
  - **AC-002.1**: **Given** `[DependentRun(DependentRunMode.MinInterval, "00:20:00")]`, **When** the plan is formed, **Then** the step's `MinInterval` is 20 minutes. Test: `DependentRunAttributeTests.Plan_ReadsMinInterval_FromAttribute`
  - **AC-002.2**: **Given** `[DependentRun(DependentRunMode.OwnSchedule)]`, **When** the plan is formed, **Then** the step `RunsOnOwnSchedule`. Test: `DependentRunAttributeTests.Plan_ReadsRunOnOwnSchedule_FromAttribute`
  - **AC-002.3**: **Given** `DependentRunMode.MinInterval` without an interval, or `DependentRunMode.OwnSchedule` with one (attribute or entry), **When** the plan is formed, **Then** it throws naming the handler. Test: `DependentRunAttributeTests.Plan_Throws_WhenModeAndIntervalDisagree`
  - **AC-002.4**: **Given** an invalid or non-positive `MinInterval` string, **When** the plan is formed, **Then** it throws naming the handler and the value. Test: `DependentRunAttributeTests.Plan_Throws_WhenMinIntervalIsInvalid`
  - **AC-002.5**: **Given** `[DependentRun]` on a handler without prerequisites, **When** the plan is formed, **Then** it throws, as 010 AC-007.4. Test: `DependentRunAttributeTests.Plan_Throws_WhenHandlerHasNoPrerequisites`

- **FR-003 Validation of the automatic plan**: cycles always fail with the cycle path. A missing or disabled prerequisite follows `ExecutionFlowOptions.AutomaticPlanMissingPrerequisite` (core, shared by every provider), default `MissingPrerequisite.Throw`, alternative `MissingPrerequisite.SkipDependents`, with the semantics of 010 FR-004. It applies only to the automatic plan; an explicit plan takes its policy from `ExecutionPlanner.Build(policy)`. *(Story 3)*
  - **AC-003.1**: **Given** `A` and `B` that depend on each other by attribute, **When** the options are configured, **Then** it throws with the path `A -> B -> A`. Test: `AutomaticPlanTests.Configure_Throws_WithCyclePath`
  - **AC-003.2**: **Given** a prerequisite that isn't registered and the default option, **When** the options are configured, **Then** it throws naming both handlers. Test: `AutomaticPlanTests.Configure_Throws_WhenPrerequisiteNotRegistered`
  - **AC-003.3**: **Given** a prerequisite turned off with `SetJobAutoRun(false)` and the default option, **When** the options are configured, **Then** it throws. Test: `AutomaticPlanHangfireTests.Configure_Throws_WhenPrerequisiteIsTurnedOff`
  - **AC-003.4**: **Given** the option set to `SkipDependents`, **When** a prerequisite is missing or off, **Then** the dependents are turned off in cascade, each with a reason. Test: `AutomaticPlanTests.Configure_TurnsOffDependentsInCascade_WhenSkipDependents`

- **FR-004 Explicit plans**: with `UsePlan`, the explicit plan is the only plan. Handlers registered outside it keep ignoring `[DependsOn]` (documented, no warning). Handlers outside it also keep ignoring `[DependentRun]`. For handlers in the explicit plan, `ExecutionPlanner` also reads `[DependentRun]`, and the entry's `RunMode(...)` wins over the attribute as a whole. *(Story 4)*
  - **AC-004.1**: **Given** an explicit plan and a registered handler outside it with `[DependsOn]`, **When** the options are configured, **Then** it runs on its own schedule and nothing throws or warns; the same for `[DependentRun]` outside the plan. Test: `AutomaticPlanHangfireTests.Configure_IgnoresDependsOn_OutsideExplicitPlan`
  - **AC-004.2**: **Given** a plan entry for a handler with `[DependentRun(DependentRunMode.MinInterval, "00:20:00")]` and no mode on the entry, **When** the plan is built, **Then** the step's `MinInterval` is 20 minutes. Test: `ExecutionPlannerTests.Build_ReadsDependentRunAttribute`
  - **AC-004.3**: **Given** the same handler with `.RunMode(DependentRunMode.OwnSchedule)` or `.RunMode(DependentRunMode.MinInterval, 10 min)` on the entry, **When** the plan is built, **Then** the entry's mode is used and the attribute's is ignored. Test: `ExecutionPlannerTests.Build_PrefersEntryRunMode_OverDependentRunAttribute`

### Business Rules

- **RN-001**: An automatic plan and an explicit plan never coexist: `UsePlan` always wins, and the automatic plan is only formed without it.
- **RN-002**: The automatic plan reuses the explicit plan's rules (ordering, validation, run modes, generations); no provider has rules specific to it, except that enabled/disabled comes from `SetJobAutoRun`/`GlobalRecurringAutoRun`.
- **RN-003**: `[DependsOn]` and the entry's `DependsOn` keep merging inside an explicit plan (010 FR-002).

### Key Entities

- **`[DependsOn]`**: a prerequisite of a recurring handler (existing, repeatable).
- **`[DependentRun]`** / **`DependentRunMode`**: the run mode of a dependent (new, one per class): `MinInterval` or `OwnSchedule`.
- **Automatic plan**: the plan formed from the registered handlers' attributes when no explicit plan is registered.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A chain of dependent handlers is configured with zero lines of planner code: only attributes and `Scan` (`AutomaticPlanHangfireTests.Build_RegistersDependent_AsTriggeredByPlan`).
- **SC-002**: Every `[DependsOn]` on a registered handler is either applied or explicitly documented as ignored (explicit plan); no configuration silently drops it without the documented rule (AC-001.1, AC-004.1).
- **SC-003**: All existing explicit-plan tests (`ExecutionPlannerTests`, `ExecutionPlanHangfireTests`, `PrerequisiteGateTests`, `RecurringRegistrationTests`) pass after moving to `RunMode`, with the same assertions.

## Assumptions

- Out of scope: configuration classes per handler (`IHandlerConfiguration<T>`, feature 014); "any of" dependencies; dependencies involving event handlers.
- The automatic plan is formed at configuration time, so configuration errors surface at startup, as with an explicit plan.
- Documentation requirement: the README explains the automatic plan, `[DependentRun]`, and that with `UsePlan` the `[DependsOn]` of handlers outside the plan is ignored.
