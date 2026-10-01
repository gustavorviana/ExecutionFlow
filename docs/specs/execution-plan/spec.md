# Spec: Execution plan (dependencies between recurring handlers)

Status: draft
Mode: to-be (v1.3.0). Design in [plan.md](plan.md).
Packages: `ExecutionFlow`

## Context
Recurring handlers are independent jobs, but their data often isn't: handler B reads or references what handler A produces, and running B before A has finished a cycle creates references to data that doesn't exist yet.

`ExecutionPlanner` lets a consumer declare, in one place, which recurring handlers exist, which are enabled, and which wait for which. `Build()` checks the declaration before anything runs and returns an immutable `ExecutionPlan` that execution providers read. This step only covers the plan itself. How providers enforce it at run time is specified separately ([ADR-0009](../adr/ADR-0009-dependencies-by-generation.md)).

## Actors
| Actor | Description |
| ----- | ----------- |
| Consumer app | Creates the planner, configures each handler, and builds the plan. |
| Handler author | Can declare prerequisites on the handler class with `[DependsOn]`. |
| Execution provider | Reads the built plan. |

## User stories
- US-001: As a consumer app, I want to declare that a recurring handler depends on others, so that it never runs on data its prerequisites haven't produced yet.
- US-002: As a consumer app, I want an invalid plan to fail at startup with a message that says what to fix, so that no handler silently stops running.
- US-003: As a consumer app, I want to turn a handler off and choose whether its dependents fail the startup or are turned off with it.
- US-004: As a provider, I want the steps in an order where every prerequisite comes first, each with everything needed to run or report it.

## Requirements

### REQ-001: Declare handlers
`ExecutionPlanner.Add<THandler>()` declares a recurring handler and returns an `ExecutionPlanEntry` for configuring it.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN the same handler is added twice, THE SYSTEM SHALL throw `InvalidOperationException` naming the handler. Test: `ExecutionPlannerTests.Add_Throws_WhenHandlerAddedTwice`
- AC-001.2: WHEN the planner is modified after `Build`, THE SYSTEM SHALL throw `InvalidOperationException`. Test: `ExecutionPlannerTests.Add_Throws_AfterBuild`

### REQ-002: Declare dependencies
Dependencies are declared with `entry.DependsOn<A>()`, or with `[DependsOn(typeof(A))]` on the handler class, which can be repeated. Both are merged, and duplicates are ignored.
Source: US-001

Acceptance criteria:
- AC-002.1: WHEN `Add<B>().DependsOn<A>()` is declared, THE SYSTEM SHALL list `A` as a prerequisite of `B`, and `B` as a dependent of `A`. Test: `ExecutionPlannerTests.Build_ListsPrerequisitesAndDependents`
- AC-002.2: WHEN the handler class has `[DependsOn(typeof(A))]`, THE SYSTEM SHALL merge it with the fluent prerequisites. Test: `ExecutionPlannerTests.Add_MergesDependsOnAttribute`
- AC-002.3: WHEN a prerequisite is declared more than once, THE SYSTEM SHALL list it once. Test: `ExecutionPlannerTests.DependsOn_IgnoresDuplicates`
- AC-002.4: WHEN a handler depends on itself, fluently or by attribute, THE SYSTEM SHALL throw `InvalidOperationException`. Tests: `ExecutionPlannerTests.DependsOn_Throws_WhenSelfDependency`, `Add_Throws_WhenAttributeIsSelfDependency`

### REQ-003: Enabled handlers and display names
Each entry is enabled by default, and `Enabled(false)` turns it off. A disabled handler stays in the plan, with `Enabled = false` and `SkipReason = "disabled"`. The step's display name comes from `DisplayName(string)`, then `[DisplayName]`, then the class name.
Source: US-003, US-004

Acceptance criteria:
- AC-003.1: WHEN an entry is `Enabled(false)`, THE SYSTEM SHALL keep it in the plan with `Enabled = false` and a "disabled" skip reason. Test: `ExecutionPlannerTests.Build_KeepsDisabledHandler_WithReason`
- AC-003.2: WHEN the display name is set fluently, by `[DisplayName]`, or not at all, THE SYSTEM SHALL resolve it in that order of precedence. Test: `ExecutionPlannerTests.Build_ResolvesDisplayName_InPrecedenceOrder`

### REQ-004: Unavailable prerequisites
A prerequisite is unavailable when it wasn't added to the planner, or when it's disabled. What `Build` does when an enabled handler has an unavailable prerequisite depends on `MissingPrerequisite`:
- `Throw` (the default) throws `InvalidOperationException`, naming both handlers and saying how to fix it.
- `SkipDependents` disables the dependent with a reason naming the prerequisite. The effect cascades down the chain.

A disabled handler with an unavailable prerequisite is never an error.
Source: US-002, US-003

Acceptance criteria:
- AC-004.1: WHEN a prerequisite wasn't added and the policy is `Throw`, THE SYSTEM SHALL throw. Test: `ExecutionPlannerTests.Build_Throws_WhenPrerequisiteNotAdded`
- AC-004.2: WHEN a prerequisite is disabled and the policy is `Throw`, THE SYSTEM SHALL throw. Test: `ExecutionPlannerTests.Build_Throws_WhenPrerequisiteDisabled`
- AC-004.3: WHEN the policy is `SkipDependents`, THE SYSTEM SHALL disable the dependents in cascade, each with a reason. Test: `ExecutionPlannerTests.Build_DisablesDependentsInCascade_WhenSkipDependents`
- AC-004.4: WHEN a disabled handler has an unavailable prerequisite, THE SYSTEM SHALL accept the plan. Test: `ExecutionPlannerTests.Build_Accepts_WhenDisabledHandlerHasUnavailablePrerequisite`

### REQ-005: Cycles
Source: US-002

Acceptance criteria:
- AC-005.1: WHEN the dependencies form a cycle, THE SYSTEM SHALL throw with the path of the cycle (`A -> B -> A`), whatever the policy. Test: `ExecutionPlannerTests.Build_Throws_WithCyclePath`

### REQ-006: Execution order and immutability
`ExecutionPlan.Steps` lists every added handler in an order where every prerequisite comes before its dependents. Wherever the dependencies allow it, the declaration order is kept. The plan and its collections are read-only.
Source: US-004

Acceptance criteria:
- AC-006.1: WHEN a handler is declared before its prerequisite, THE SYSTEM SHALL order the prerequisite first. Test: `ExecutionPlannerTests.Build_OrdersPrerequisitesFirst`
- AC-006.2: WHEN handlers are independent, THE SYSTEM SHALL keep their declaration order. Test: `ExecutionPlannerTests.Build_KeepsDeclarationOrder_WhenIndependent`
- AC-006.3: WHEN the plan's collections are read, THE SYSTEM SHALL return them as read-only. Test: `ExecutionPlannerTests.Build_ReturnsReadOnlyCollections`

### REQ-007: Run mode of a dependent
A dependent (an entry with prerequisites) runs in one of three modes. Providers enforce them.
- **Default:** it's triggered as soon as every prerequisite has completed a new cycle.
- **`MinInterval(TimeSpan)`:** triggered the same way, but never less than the interval after its previous run finished. A trigger that comes too early is postponed to the end of the interval, not lost.
- **`RunOnOwnSchedule()`:** it runs on its own schedule. At each occurrence it runs only if every prerequisite has a new cycle, and otherwise records that its prerequisites weren't met.

A manual run of a dependent ignores the mode and the prerequisites. It still counts as a run: it consumes the prerequisites' cycles, and its end starts the `MinInterval`.
Source: US-001

Acceptance criteria:
- AC-007.1: WHEN an entry sets `MinInterval` or `RunOnOwnSchedule`, THE SYSTEM SHALL expose it on its step (`MinInterval`, `RunsOnOwnSchedule`). Test: `ExecutionPlannerTests.Build_ExposesRunMode`
- AC-007.2: WHEN `MinInterval` is zero or negative, THE SYSTEM SHALL throw `ArgumentOutOfRangeException`. Test: `ExecutionPlannerTests.MinInterval_Throws_WhenNotPositive`
- AC-007.3: WHEN an entry sets both `MinInterval` and `RunOnOwnSchedule`, THE SYSTEM SHALL throw `InvalidOperationException` at `Build`. Test: `ExecutionPlannerTests.Build_Throws_WhenMinIntervalAndOwnScheduleAreCombined`
- AC-007.4: WHEN an entry without prerequisites sets `MinInterval` or `RunOnOwnSchedule`, THE SYSTEM SHALL throw `InvalidOperationException` at `Build`. Test: `ExecutionPlannerTests.Build_Throws_WhenRunModeIsSetWithoutPrerequisites`

## Business rules
- RN-001 A dependency applies to every run of the dependent, not just the first ([ADR-0009](../adr/ADR-0009-dependencies-by-generation.md)). It is enforced by providers.
- RN-002 Only recurring handlers (`IHandler`) can be added.

## Edge cases and errors
- An empty planner: `Build` returns an empty plan.
- An unavailable prerequisite of a disabled handler: no error, and the handler is reported as "disabled".

## Non-functional requirements
- NFR-001: `Build` needs no host, container or storage. It only looks at types.

## Out of scope
- Integration with `ExecutionFlowOptions`/`ExecutionFlowSetup` and with the providers (later steps).
- Dependencies involving event handlers.
- "Any of" dependencies.

## Open questions
- None.
