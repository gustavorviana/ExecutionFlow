---
description: "Tasks for dependencies by attribute (automatic execution plan), regenerated after /speckit-analyze"
---

# Tasks: Dependencies by attribute (automatic execution plan)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [quickstart.md](quickstart.md)

**Tests**: mandatory (constitution VII). Each story writes its tests first; a task is done when the tests of the acceptance scenarios it covers pass, together with the existing suite (SC-003).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

No setup: the projects and test projects already exist.

## Phase 2: Foundational

- [ ] T001 Extract the `[d.]hh:mm:ss` parser of `ExecutionFlowOptions.ParseInterval` into an internal helper in `Src/ExecutionFlow/Attributes/TimeSpanText.cs`, returning the value or an error message, so `[RecurringInterval]` (minimum one minute) and `[DependentRun]` (positive only, R-004) share it; every `IntervalScheduleTests` passes unchanged
- [ ] T002 Keep the registration order in `ExecutionFlowOptions` (internal list next to the dictionary, re-adding a type keeps its first position) with a test `AutomaticPlanTests.Options_KeepRegistrationOrder` in `Tests/ExecutionFlow.Tests/AutomaticPlanTests.cs` (FR-001 "registration order", analysis U1)

## Phase 3: User Story 2 - One run mode, in the attribute and in the plan (Priority: P1)

**Goal**: `DependentRunMode`, `[DependentRun(mode, interval?)]` and `ExecutionPlanEntry.RunMode(mode, interval?)` replacing `MinInterval()`/`RunOnOwnSchedule()`; the planner reads the attribute and the entry wins.

**Independent Test**: a dependent with `[DependentRun(DependentRunMode.MinInterval, "00:20:00")]` exposes a 20-minute `MinInterval`; with `.RunMode(DependentRunMode.OwnSchedule)` on its entry it `RunsOnOwnSchedule` instead.

- [ ] T003 [P] [US2] Tests `DependentRunAttributeTests` (`Plan_ReadsMinInterval_FromAttribute`, `Plan_ReadsRunOnOwnSchedule_FromAttribute`, `Plan_Throws_WhenModeAndIntervalDisagree`, `Plan_Throws_WhenMinIntervalIsInvalid`, `Plan_Throws_WhenHandlerHasNoPrerequisites`) in `Tests/ExecutionFlow.Tests/Attributes/DependentRunAttributeTests.cs`, and `ExecutionPlannerTests.Build_ReadsDependentRunAttribute` / `Build_PrefersEntryRunMode_OverDependentRunAttribute` in `Tests/ExecutionFlow.Tests/Abstractions/ExecutionPlannerTests.cs` (AC-002.1..AC-002.5, AC-004.2, AC-004.3)
- [ ] T004 [US2] (`DependentRunMode` already created by 014 T001) Create `DependentRunAttribute(DependentRunMode mode, string interval = null)` ("`[d.]hh:mm:ss`, positive; required for MinInterval, forbidden for OwnSchedule", `AllowMultiple = false`) with XML docs in `Src/ExecutionFlow/Attributes/DependentRunAttribute.cs` (FR-002; depends on T001)
- [X] T005 [US2] ~~Done by 014 T002~~ (`HandlerBuilder.RunMode` replaces `ExecutionPlanEntry`). Originally: In `Src/ExecutionFlow/Abstractions/ExecutionPlanEntry.cs`: add `RunMode(DependentRunMode, TimeSpan? interval = null)` (positive interval, required for MinInterval, forbidden for OwnSchedule) and **remove** `MinInterval(TimeSpan)` and `RunOnOwnSchedule()` (confirmed removal, spec Clarifications); keep the read-only `ConfiguredMinInterval`/`RunsOnOwnSchedule`; keep the attribute mode apart (FR-002; depends on T004)
- [ ] T006 [US2] In `Src/ExecutionFlow/Abstractions/ExecutionPlanner.cs` (`Register`) and `HandlerBuilder.cs`: read `[DependentRun]` when a handler is registered; at `Build`, use the builder's mode if `RunMode` was called, else the attribute's (R-002); validate (mode without prerequisites, mode/interval mismatch, invalid string naming handler and value) (FR-002, FR-004; depends on T003, T005)
- [X] T007 [US2] ~~Done by 014 T003~~. Originally: Move every caller of the removed methods to `RunMode`: `Tests/ExecutionFlow.Tests/Abstractions/ExecutionPlannerTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/ExecutionPlanHangfireTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/PrerequisiteGateTests.cs`, `Tests/ExecutionFlow.Hangfire.Tests/RecurringRegistrationTests.cs`, `Examples/ExecutionFlow.Examples.Handlers/Recurring/Plan/CatalogPlan.cs`; rename the 010 tests that named the old methods and update their names in `specs/010-execution-plan/spec.md` AC-007.1..AC-007.4 with a one-line amendment note (depends on T005)

**Checkpoint**: the build is green with `RunMode`; every existing plan test passes after the rename.

## Phase 4: User Story 1 - Automatic plan from attributes (Priority: P1) 🎯 MVP

**Goal**: without `UsePlan`, registered handlers with `[DependsOn]` or `[DependentRun]` form a plan that Hangfire applies.

**Independent Test**: `A` and `B` with `[DependsOn(typeof(A))]` registered by `Scan`, no `UsePlan`: `B` is registered with `Cron.Never()` and triggered by `A`.

- [ ] T008 [P] [US1] Tests `AutomaticPlanTests.Configure_BuildsAutomaticPlan_FromDependsOnAttributes`, `Configure_FormsNoPlan_WhenNoHandlerHasDependsOn`, `Configure_FormsPlan_WhenOnlyDependentRunIsPresent` (fails per AC-002.5) in `Tests/ExecutionFlow.Tests/AutomaticPlanTests.cs`; `AutomaticPlanHangfireTests.Build_RegistersDependent_AsTriggeredByPlan` and `Configure_TakesEnabledFromSetJobAutoRun` in `Tests/ExecutionFlow.Hangfire.Tests/AutomaticPlanHangfireTests.cs` (AC-001.1..AC-001.5, SC-001)
- [ ] T009 [US1] In `Src/ExecutionFlow/ExecutionFlowOptions.cs`: `AutomaticPlanMissingPrerequisite` (default `MissingPrerequisite.Throw`, throws when locked), `protected internal virtual bool IsRecurringHandlerEnabled(Type)` (default `true`), `protected virtual void OnAutomaticPlanFormed(ExecutionPlan)`, and an internal `SetAutomaticPlan(ExecutionPlan)` that bypasses `OnPlanRegistering` (R-003); `IsRecurringHandlerEnabled` already exists (014), and `ResolvePlan` already re-evaluates enabled steps (FR-001, FR-003; depends on T002, T006)
- [ ] T010 [US1] In `Src/ExecutionFlow/Abstractions/ExecutionFlowSetup.cs` `Configure`: after `configure(Options)` and before `Lock()`, when `Options.Plan == null` and some registered recurring handler has `[DependsOn]` or `[DependentRun]`, add every registered recurring handler to an `ExecutionPlanner` in registration order with `.Enabled(Options.IsRecurringHandlerEnabled(type))`, `Build(Options.AutomaticPlanMissingPrerequisite)` and `SetAutomaticPlan` (FR-001, R-001; depends on T009)
- [ ] T011 [US1] In `Src/ExecutionFlow.Hangfire/HangfireOptions.cs`: `IsRecurringHandlerEnabled` is already overridden (014) and `RecurringJobResolver.IsAutoRun` already reads the plan's steps, so only check that the automatic plan needs nothing more (`OnAutomaticPlanFormed` may be unnecessary) (FR-001, AC-001.4; depends on T010)

**Checkpoint**: MVP — a dependency chain works with attributes and `Scan` only.

## Phase 5: User Story 3 - Invalid dependencies fail at startup (Priority: P1)

**Goal**: cycles and missing or turned-off prerequisites in the automatic plan fail, or cascade with `SkipDependents`.

**Independent Test**: two handlers that depend on each other by attribute make `Configure` throw with `A -> B -> A`.

- [ ] T012 [P] [US3] Tests `AutomaticPlanTests.Configure_Throws_WithCyclePath`, `Configure_Throws_WhenPrerequisiteNotRegistered`, `Configure_TurnsOffDependentsInCascade_WhenSkipDependents` in `Tests/ExecutionFlow.Tests/AutomaticPlanTests.cs`, and `AutomaticPlanHangfireTests.Configure_Throws_WhenPrerequisiteIsTurnedOff` in `Tests/ExecutionFlow.Hangfire.Tests/AutomaticPlanHangfireTests.cs` (AC-003.1..AC-003.4)
- [ ] T013 [US3] Make the T012 tests pass; validation comes from `ExecutionPlanner.Build`, so adjust only error messages or `HangfireOptions.OnAutomaticPlanFormed` if needed (FR-003; depends on T011, T012)

## Phase 6: User Story 4 - Explicit plans keep working (Priority: P2)

**Goal**: with `UsePlan`, the explicit plan is the only one; `[DependsOn]`/`[DependentRun]` outside it are ignored.

**Independent Test**: an explicit plan plus a registered handler outside it with `[DependsOn]` and `[DependentRun]`: that handler keeps its own cron, nothing throws.

- [ ] T014 [US4] Test `AutomaticPlanHangfireTests.Configure_IgnoresDependsOn_OutsideExplicitPlan` (covering `[DependsOn]` and `[DependentRun]`) in `Tests/ExecutionFlow.Hangfire.Tests/AutomaticPlanHangfireTests.cs`, and confirm `ExecutionPlanHangfireTests`, `PrerequisiteGateTests`, `ExecutionPlannerTests` pass (AC-004.1, SC-003; depends on T011)

## Phase 7: Polish & Cross-Cutting

- [ ] T015 [P] README (root and core): automatic plan, `[DependentRun(DependentRunMode, ...)]`, `RunMode` in the plan (replacing `MinInterval()`/`RunOnOwnSchedule()`), `AutomaticPlanMissingPrerequisite`, and that with `UsePlan` the attributes of handlers outside the plan are ignored
- [ ] T016 [P] Amendment notes: under 010 FR-007 and 005 FR-009 pointing to 013; in `specs/011-local-hosting/spec.md` FR-002/FR-005 that the local host applies the automatic plan; in `specs/011-local-hosting/tasks.md` T003 add the `HostingOptions` overrides of `IsRecurringHandlerEnabled`/`OnAutomaticPlanFormed` with a test (analysis C1)
- [ ] T017 Run `quickstart.md` scenarios 1-4 and `/speckit-analyze`

## Dependencies & Execution Order

- T001, T002 → T004 → T005 → T006, T007 → T009 → T010 → T011 → T013 / T014 → T015..T017
- Story order: US2 (single run mode, planner reads the attribute) → US1 (MVP) → US3 → US4. US2 comes first because US1's automatic plan uses it and T007 must keep the build green.

## Parallel Opportunities

- T003, T008 and T012 (test files) can be written in parallel with the foundational tasks.
- T015 and T016 in parallel at the end.

## Implementation Strategy

1. MVP: T001..T011.
2. Validation (T012, T013) and explicit-plan safety (T014).
3. Docs and amendments (T015, T016), then validation (T017).
