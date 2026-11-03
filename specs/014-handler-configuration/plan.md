# Implementation Plan: Handler configuration classes (EF Core style)

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-11-02 (revised for FR-005/FR-006) | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-handler-configuration/spec.md`. Decisions in [research.md](research.md).

## Summary

`ExecutionPlanner` is the single place where handlers are configured, through one builder, `HandlerBuilder<T>` (FR-001..FR-004, implemented). Two planners feed each provider's options (FR-006):

- the **internal planner**: the class's attributes plus `options.GetPlanOf<T>()`, the options layer (the obsolete `SetJobAutoRun`/`SetJobTimeZone` call it). Without a user plan it decides each handler's items and forms the automatic plan (013);
- the **user planner**: `UsePlan(...)` / `UsePlanFromAssembly(...)` (FR-005) / configuration classes. A handler in it drops its internal-planner `GetPlanOf` values as a whole (with a warning); its attributes still apply item by item.

Providers read one resolved view per handler, so Hangfire and the local host behave the same.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23

**Storage**: N/A

**Testing**: xUnit + NSubstitute; trace capture as in `IntervalRegistrationTests`

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library

**Performance Goals**: N/A (configuration time)

**Constraints**: core dependency-free; v1.0.2 API kept (obsolete, not removed); behavior changes only as confirmed in the spec

**Scale/Scope**: tens of handlers

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

- I. Dependency-free core: ✅ planners, builder, options layer and resolution live in the core; Hangfire only reads the resolved view and adds its extensions.
- II. Processors isolated: ✅ the same core resolution serves Hangfire and the local host (011).
- V. Breaking changes: ⚠️ `ExecutionPlanEntry` removed and the `SetJobAutoRun` + plan error turned into "user plan replaces the options layer" (1.3.0, unreleased, confirmed in spec Clarifications). `SetJobAutoRun`/`SetJobTimeZone` become `[Obsolete]`, not removed, so v1.0.2 code compiles and behaves the same without a plan. New public API: `GetPlanOf<T>()`, `GetPlanOf(Type)` (research R-008), `UsePlanFromAssembly`, `ApplyConfigurationsFromAssembly(assembly, predicate)`.
- VII. Tested, traceable behavior: ✅ every active AC names a test; tests of the deprecated AC-002.6/AC-002.7 are removed or converted (T022).

Post-design re-check: ✅ unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/014-handler-configuration/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/public-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `HandlerBuilder` (abstract) / `HandlerBuilder<T>`, `IHandlerConfiguration<T>`, planner `Apply…`; `ExecutionFlowOptions.UsePlanFromAssembly`, `GetPlanOf<T>()`, `GetPlanOf(Type)`, internal planner and per-handler resolution | 1.3.0 only (confirmed) |
| `ExecutionFlow.Hangfire` | `SetJobAutoRun`/`SetJobTimeZone` `[Obsolete]` → `GetPlanOf`; `RecurringAutoRun`/`RecurringTimeZones` dictionaries replaced by the core resolution; `Queue`/`AutomaticRetry` extensions | No (obsolete only) |

```text
Src/ExecutionFlow/ExecutionFlowOptions.cs                       # internal planner, GetPlanOf, ResolvePlan
Src/ExecutionFlow/Abstractions/HandlerBuilder.cs, ExecutionPlanner.cs, ExecutionPlan.cs
Src/ExecutionFlow.Hangfire/HangfireOptions.cs, HangfireSetup.cs
Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs
Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs, AutomaticPlanTests.cs
Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs, ExecutionPlanHangfireTests.cs
```

## Design

### Builders and planners (implemented: FR-001..FR-005, T021)

- `HandlerBuilder` is abstract with an internal constructor; `HandlerBuilder<T>` is the only concrete builder. Every call records an explicit value per item.
- `ExecutionPlanner.Add<T>()`/`Add(Type)` return the builder; `ApplyConfiguration`, `ApplyConfigurationsFromAssembly(assembly[, predicate])`; configuring a handler twice throws.
- `ExecutionFlowOptions.UsePlanFromAssembly(assembly, predicate, policy)` = `UsePlan(new ExecutionPlanner().ApplyConfigurationsFromAssembly(...).Build(policy))`.

### Internal planner and options layer (FR-006, to implement)

- `ExecutionFlowOptions` owns an internal `ExecutionPlanner` that is never built directly. `GetPlanOf<T>()` / `GetPlanOf(Type)` return that planner's builder for the handler, created on the first call (`Add`, which also loads the class's `[DependsOn]`/`[DependentRun]`); `ThrowIfLocked` applies. A `GetPlanOf` for a type that isn't a registered recurring handler throws at configuration, like `SetJobAutoRun` does today (R-007).
- The **effective builder** of a handler is: the user plan's step when the handler is in the user plan; otherwise its internal `GetPlanOf` builder, if any; otherwise none (attributes and globals only).
- `ResolvePlan` (at `Configure`, before `Lock`):
  1. **User plan present**: for each handler in it that also has `GetPlanOf` values, write a trace warning naming the handler (R-006). Merge the user step's schedule/Id/name into the registration (as today). Re-resolve enabled steps with `ConfiguredEnabled ?? global` (the options layer never counts for them) and the plan's policy (R-003).
  2. **No user plan, internal planner has dependencies or run modes** (attributes or `GetPlanOf`): form the automatic plan from every registered recurring handler (013), using the `GetPlanOf` builder where it exists, `Build(AutomaticPlanMissingPrerequisite)`, then re-resolve with `ConfiguredEnabled ?? global`.
  3. **Otherwise**: no plan; each handler with a `GetPlanOf` builder has its schedule/Id/name merged into the registration.
- Providers ask the core for the per-handler items they need: `IsEnabled(type)` (plan step → `Enabled`; else `GetPlanOf.ConfiguredEnabled ?? global`) and `ResolveTimeZoneId(type)` (plan step → `TimeZone`; else `GetPlanOf.ConfiguredTimeZone`; then attribute; then global; intervals → none). The global defaults stay provider options (`GlobalRecurringAutoRun`, `RecurringTimeZone`), passed through a protected virtual (R-009).
- **Hangfire**: `SetJobAutoRun(type, x)` → `GetPlanOf(type).Enabled(x)`; `SetJobTimeZone(type, tz)` → `GetPlanOf(type).TimeZone(tz)`, both `[Obsolete]`. `RecurringJobResolver` uses the core resolution; `RecurringAutoRun`/`RecurringTimeZones` are removed (internal). The existing "references a type that isn't registered" errors move to the `GetPlanOf` check. The interval + time-zone warning (012) reads the effective builder's time zone.

## Complexity Tracking

| Item | Why | Simpler alternative rejected |
|---|---|---|
| Internal planner kept apart from the user planner | maintainer decision: a handler in the user plan drops the options layer as a whole while attributes keep applying | one merged planner (would mix options into user plans, the deprecated AC-002.6) |
| Enabled re-resolution with the global default (R-003) | the global `GlobalRecurringAutoRun` and the options layer are known only at `Configure`, after the plan was built | resolving at `Build` (options not available yet) |
