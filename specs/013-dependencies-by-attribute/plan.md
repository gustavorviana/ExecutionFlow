# Implementation Plan: Dependencies by attribute (automatic execution plan)

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-dependencies-by-attribute/spec.md`. Decisions in [research.md](research.md).

## Summary

Add `[DependentRun(DependentRunMode, interval?)]` to the core, replace 010's `ExecutionPlanEntry.MinInterval()`/`RunOnOwnSchedule()` with a single `RunMode(DependentRunMode, TimeSpan?)`, and teach `ExecutionPlanner` to read the attribute (the entry wins). When the options have no explicit plan and a registered recurring handler has `[DependsOn]` or `[DependentRun]`, `ExecutionFlowSetup.Configure` forms an automatic plan by feeding every registered recurring handler to an `ExecutionPlanner`, with each handler's enabled state asked from the provider, and builds it with `ExecutionFlowOptions.AutomaticPlanMissingPrerequisite`. Providers then see an ordinary `Options.Plan` and apply it with their existing machinery.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23

**Storage**: N/A (the automatic plan reuses the explicit plan's Hangfire storage, 010 R-003)

**Testing**: xUnit + NSubstitute

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library

**Performance Goals**: N/A (runs once in `Configure`)

**Constraints**: no change to explicit-plan behavior (SC-003); core stays dependency-free

**Scale/Scope**: tens of recurring handlers

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

- I. Dependency-free core: ✅ attribute, planner and automatic-plan formation are core-only; providers only answer "is this handler enabled".
- II. Processors isolated: ✅ Hangfire keeps its gate, triggers and auto-run; it only maps the automatic plan's disabled steps to `Cron.Never()`.
- V. Minimal public surface / breaking changes: ⚠️ adds `DependentRunAttribute`, `DependentRunMode`, `ExecutionPlanEntry.RunMode`, `ExecutionFlowOptions.AutomaticPlanMissingPrerequisite` and two protected virtuals; **removes** `ExecutionPlanEntry.MinInterval()` and `RunOnOwnSchedule()` (010, unreleased 1.3.0). The removal is confirmed by the maintainer and recorded in spec Clarifications (2026-11-02); nothing published breaks, so no major bump. Behavior change: `[DependsOn]` outside a plan is no longer ignored when no `UsePlan` exists; `[DependsOn]` shipped only in the unreleased 1.3.0, and the maintainer decided it (spec Clarifications, 2026-11-02). No major bump needed.
- VI. Dispatch doesn't block: ✅ not touched.
- VII. Tested, traceable behavior: ✅ every AC names a test; existing plan tests must pass unchanged (SC-003).

Post-design re-check: ✅ unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/013-dependencies-by-attribute/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/public-api.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | New `DependentRunAttribute`; `ExecutionPlanner.Add` reads it; `ExecutionFlowOptions.AutomaticPlanMissingPrerequisite`; automatic plan formed in `ExecutionFlowSetup.Configure` | No |
| `ExecutionFlow.Hangfire` | `HangfireOptions` answers enabled state (`SetJobAutoRun` > `GlobalRecurringAutoRun`) and turns off the steps the automatic plan skipped | No |

```text
Src/ExecutionFlow/Attributes/DependentRunAttribute.cs
Src/ExecutionFlow/Abstractions/ExecutionPlanner.cs, ExecutionPlanEntry.cs, ExecutionFlowSetup.cs
Src/ExecutionFlow/ExecutionFlowOptions.cs
Src/ExecutionFlow.Hangfire/HangfireOptions.cs
Tests/ExecutionFlow.Tests/…/DependentRunAttributeTests.cs, AutomaticPlanTests.cs, ExecutionPlannerTests.cs
Tests/ExecutionFlow.Hangfire.Tests/AutomaticPlanHangfireTests.cs
```

## Design

- **`DependentRunMode`** enum: `MinInterval`, `OwnSchedule`. **`[DependentRun(DependentRunMode mode, string interval = null)]`**: `interval` is `[d.]hh:mm:ss`, parsed with the shared parser, positive only (R-004); required for `MinInterval`, forbidden for `OwnSchedule`.
- **`ExecutionPlanEntry.RunMode(DependentRunMode mode, TimeSpan? interval = null)`** replaces `MinInterval(TimeSpan)` and `RunOnOwnSchedule()`; the read-only `ConfiguredMinInterval`/`RunsOnOwnSchedule` properties stay. `ExecutionPlanStep` is unchanged (`MinInterval`, `RunsOnOwnSchedule`). 010's tests, `Examples/…/CatalogPlan.cs` and the README move to `RunMode`.
- **Entry vs attribute** (R-002): an entry `RunMode(...)` replaces the attribute's mode as a whole; otherwise the attribute's mode is used. `Build` validates the result with the existing rules (mode without prerequisites → error); a mode/interval mismatch or an invalid interval string throws naming the handler and the value.
- **Automatic plan** (R-001): in `ExecutionFlowSetup.Configure`, after `configure(Options)` and before `Lock()`:
  1. if `Options.Plan != null` or no registered recurring handler has `[DependsOn]` or `[DependentRun]`, do nothing;
  2. otherwise create an `ExecutionPlanner`, `Add` every registered recurring handler in registration order (the options keep that order in a list next to the dictionary, U1), set `.Enabled(Options.IsRecurringHandlerEnabled(type))`, and `Build(Options.AutomaticPlanMissingPrerequisite)`;
  3. store it with an internal `SetAutomaticPlan(plan)` that bypasses `OnPlanRegistering` (no `SetJobAutoRun` conflict) and calls `protected virtual OnAutomaticPlanFormed(plan)`.
- **`ExecutionFlowOptions`**: `protected internal virtual bool IsRecurringHandlerEnabled(Type)` (default `true`), `protected virtual void OnAutomaticPlanFormed(ExecutionPlan)` (default nothing). `SetJobAutoRun` needs no change: it only checks the plan when called, and the automatic plan is formed after the callback.
- **`HangfireOptions`** (and `HostingOptions` in 011): overrides `IsRecurringHandlerEnabled` with `RecurringJobResolver.IsAutoRun`, and `OnAutomaticPlanFormed` to set `RecurringAutoRun[t] = false` for steps disabled by `SkipDependents`. Everything else (`IsTriggeredByPlan`, gate filter, triggers, naming) already reads `Options.Plan`.
- **Explicit plan**: unchanged; `[DependsOn]` outside it stays ignored (FR-004).

## Complexity Tracking

No constitution violations.
