# Feature Specification: Execution plan (dependencies between recurring handlers)

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-09-30

**Status**: Implemented (v1.3.0, unreleased)

**Input**: Migrated from `docs/specs/execution-plan/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` and `NFR` numbers kept).

**Packages**: `ExecutionFlow`

**Amends**: none. Run-time enforcement in Hangfire is specified in [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-009..FR-011, and locally in [011-local-hosting](../011-local-hosting/spec.md).

## Context

Recurring handlers are independent jobs, but their data often isn't: handler B reads or references what handler A produces, and running B before A has finished a cycle creates references to data that doesn't exist yet.

`ExecutionPlanner` lets a consumer declare, in one place, which recurring handlers exist, which are enabled, and which wait for which. `Build()` checks the declaration before anything runs and returns an immutable `ExecutionPlan` that execution providers read. This feature only covers the plan itself; how providers enforce it at run time is decided in [research.md](research.md) (R-003, was ADR-0009).

**Actors**: the consumer app (creates the planner, configures each handler and builds the plan), the handler author (can declare prerequisites with `[DependsOn]`), and the execution provider (reads the built plan).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare that a recurring handler depends on others (Priority: P1)

As a consumer app, I want to declare that a recurring handler depends on others, so that it never runs on data its prerequisites haven't produced yet.

**Why this priority**: it is the reason the feature exists.

**Independent Test**: build a planner with `Add<B>().DependsOn<A>()` and check the step's prerequisites and dependents.

**Acceptance Scenarios**: AC-001.1, AC-001.2, AC-002.1..AC-002.4, AC-007.1..AC-007.4 (see Requirements).

---

### User Story 2 - Fail at startup on an invalid plan (Priority: P1)

As a consumer app, I want an invalid plan to fail at startup with a message that says what to fix, so that no handler silently stops running.

**Why this priority**: a silently broken plan stops jobs without anyone noticing.

**Independent Test**: build plans with a cycle and with a missing prerequisite and check the exceptions.

**Acceptance Scenarios**: AC-004.1, AC-004.2, AC-005.1.

---

### User Story 3 - Turn a handler off (Priority: P2)

As a consumer app, I want to turn a handler off and choose whether its dependents fail the startup or are turned off with it.

**Why this priority**: operational control; the plan works without it.

**Independent Test**: disable a prerequisite and build with each `MissingPrerequisite` policy.

**Acceptance Scenarios**: AC-003.1, AC-004.3, AC-004.4.

---

### User Story 4 - Read an ordered, complete plan (Priority: P2)

As a provider, I want the steps in an order where every prerequisite comes first, each with everything needed to run or report it.

**Why this priority**: providers depend on it, but consumers only see it indirectly.

**Independent Test**: build a plan declared out of order and read `Steps`.

**Acceptance Scenarios**: AC-003.2, AC-006.1..AC-006.3.

### Edge Cases

- An empty planner: `Build` returns an empty plan.
- An unavailable prerequisite of a disabled handler: no error, and the handler is reported as "disabled".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Declare handlers**: `ExecutionPlanner.Add<THandler>()` declares a recurring handler and returns an `ExecutionPlanEntry` for configuring it. *(Story 1)*
  - *Amended by [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-003: an entry can turn overlap off; the entry wins over the attribute.*
  - **AC-001.1**: **Given** a handler already added, **When** it is added again, **Then** `InvalidOperationException` is thrown naming the handler. Test: `ExecutionPlannerTests.Add_Throws_WhenHandlerAddedTwice`
  - **AC-001.2**: **Given** a built planner, **When** it is modified, **Then** `InvalidOperationException` is thrown. Test: `ExecutionPlannerTests.Add_Throws_AfterBuild`

- **FR-002 Declare dependencies**: dependencies are declared with `entry.DependsOn<A>()`, or with `[DependsOn(typeof(A))]` on the handler class, which can be repeated. Both are merged, and duplicates are ignored. *(Story 1)*
  - **AC-002.1**: **Given** `Add<B>().DependsOn<A>()`, **When** the plan is built, **Then** `A` is a prerequisite of `B` and `B` is a dependent of `A`. Test: `ExecutionPlannerTests.Build_ListsPrerequisitesAndDependents`
  - **AC-002.2**: **Given** a handler class with `[DependsOn(typeof(A))]`, **When** it is added, **Then** the attribute is merged with the fluent prerequisites. Test: `ExecutionPlannerTests.Add_MergesDependsOnAttribute`
  - **AC-002.3**: **Given** a prerequisite declared more than once, **When** the plan is built, **Then** it is listed once. Test: `ExecutionPlannerTests.DependsOn_IgnoresDuplicates`
  - **AC-002.4**: **Given** a handler that depends on itself, fluently or by attribute, **When** it is declared, **Then** `InvalidOperationException` is thrown. Tests: `ExecutionPlannerTests.DependsOn_Throws_WhenSelfDependency`, `ExecutionPlannerTests.Add_Throws_WhenAttributeIsSelfDependency`

- **FR-003 Enabled handlers and display names**: each entry is enabled by default, and `Enabled(false)` turns it off. A disabled handler stays in the plan, with `Enabled = false` and `SkipReason = "disabled"`. The step's display name comes from `DisplayName(string)`, then `[DisplayName]`, then the class name. *(Stories 3, 4)*
  - **AC-003.1**: **Given** an entry with `Enabled(false)`, **When** the plan is built, **Then** it stays in the plan with `Enabled = false` and a "disabled" skip reason. Test: `ExecutionPlannerTests.Build_KeepsDisabledHandler_WithReason`
  - **AC-003.2**: **Given** a display name set fluently, by `[DisplayName]`, or not at all, **When** the plan is built, **Then** it is resolved in that order of precedence. Test: `ExecutionPlannerTests.Build_ResolvesDisplayName_InPrecedenceOrder`

- **FR-004 Unavailable prerequisites**: a prerequisite is unavailable when it wasn't added to the planner, or when it's disabled. When an enabled handler has an unavailable prerequisite, `Build` follows `MissingPrerequisite`: `Throw` (the default) throws `InvalidOperationException` naming both handlers and saying how to fix it; `SkipDependents` disables the dependent with a reason naming the prerequisite, cascading down the chain. A disabled handler with an unavailable prerequisite is never an error. *(Stories 2, 3)*
  - **AC-004.1**: **Given** a prerequisite that wasn't added and the `Throw` policy, **When** the plan is built, **Then** it throws. Test: `ExecutionPlannerTests.Build_Throws_WhenPrerequisiteNotAdded`
  - **AC-004.2**: **Given** a disabled prerequisite and the `Throw` policy, **When** the plan is built, **Then** it throws. Test: `ExecutionPlannerTests.Build_Throws_WhenPrerequisiteDisabled`
  - **AC-004.3**: **Given** the `SkipDependents` policy, **When** the plan is built, **Then** the dependents are disabled in cascade, each with a reason. Test: `ExecutionPlannerTests.Build_DisablesDependentsInCascade_WhenSkipDependents`
  - **AC-004.4**: **Given** a disabled handler with an unavailable prerequisite, **When** the plan is built, **Then** the plan is accepted. Test: `ExecutionPlannerTests.Build_Accepts_WhenDisabledHandlerHasUnavailablePrerequisite`

- **FR-005 Cycles**: dependency cycles are rejected whatever the policy. *(Story 2)*
  - **AC-005.1**: **Given** dependencies that form a cycle, **When** the plan is built, **Then** it throws with the path of the cycle (`A -> B -> A`). Test: `ExecutionPlannerTests.Build_Throws_WithCyclePath`

- **FR-006 Execution order and immutability**: `ExecutionPlan.Steps` lists every added handler in an order where every prerequisite comes before its dependents. Wherever the dependencies allow it, the declaration order is kept. The plan and its collections are read-only. *(Story 4)*
  - **AC-006.1**: **Given** a handler declared before its prerequisite, **When** the plan is built, **Then** the prerequisite comes first. Test: `ExecutionPlannerTests.Build_OrdersPrerequisitesFirst`
  - **AC-006.2**: **Given** independent handlers, **When** the plan is built, **Then** their declaration order is kept. Test: `ExecutionPlannerTests.Build_KeepsDeclarationOrder_WhenIndependent`
  - **AC-006.3**: **Given** a built plan, **When** its collections are read, **Then** they are read-only. Test: `ExecutionPlannerTests.Build_ReturnsReadOnlyCollections`

- **FR-007 Run mode of a dependent**: a dependent (an entry with prerequisites) runs in one of three modes, enforced by providers. **Default:** triggered as soon as every prerequisite has completed a new cycle. **`MinInterval(TimeSpan)`:** triggered the same way, but never less than the interval after its previous run finished; a trigger that comes too early is postponed to the end of the interval, not lost. **`RunOnOwnSchedule()`:** runs on its own schedule, and at each occurrence runs only if every prerequisite has a new cycle, otherwise records that its prerequisites weren't met. A manual run ignores the mode and the prerequisites, but still counts as a run: it consumes the prerequisites' cycles, and its end starts the `MinInterval`. *(Story 1)*
  - **AC-007.1**: **Given** an entry with `MinInterval` or `RunOnOwnSchedule`, **When** the plan is built, **Then** the step exposes it (`MinInterval`, `RunsOnOwnSchedule`). Test: `ExecutionPlannerTests.Build_ExposesRunMode`
  - **AC-007.2**: **Given** a zero or negative interval, **When** `MinInterval` is set, **Then** `ArgumentOutOfRangeException` is thrown. Test: `ExecutionPlannerTests.MinInterval_Throws_WhenNotPositive`
  - **AC-007.3**: **Given** an entry with both `MinInterval` and `RunOnOwnSchedule`, **When** the plan is built, **Then** `InvalidOperationException` is thrown. Test: `ExecutionPlannerTests.Build_Throws_WhenMinIntervalAndOwnScheduleAreCombined`
  - **AC-007.4**: **Given** an entry without prerequisites that sets `MinInterval` or `RunOnOwnSchedule`, **When** the plan is built, **Then** `InvalidOperationException` is thrown. Test: `ExecutionPlannerTests.Build_Throws_WhenRunModeIsSetWithoutPrerequisites`

### Business Rules

- **RN-001**: A dependency applies to every run of the dependent, not just the first ([research.md](research.md) R-003). It is enforced by providers.
- **RN-002**: Only recurring handlers (`IHandler`) can be added.

### Key Entities

- **ExecutionPlanner**: mutable declaration of entries, frozen by `Build`.
- **ExecutionPlanEntry**: one handler's configuration (prerequisites, enabled, display name, run mode).
- **ExecutionPlan / ExecutionPlanStep**: the immutable, ordered result read by providers.
- **MissingPrerequisite**: the policy for unavailable prerequisites (`Throw`, `SkipDependents`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001): `Build` needs no host, container or storage; it only looks at types.

## Assumptions

- Out of scope: event handlers as prerequisites or dependents; "any of" dependencies. Integration with `ExecutionFlowOptions`/`ExecutionFlowSetup` and the providers is covered by 005 and 011.
- Open questions: none.
