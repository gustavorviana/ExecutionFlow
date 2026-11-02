# Implementation Plan: Handler configuration classes (EF Core style)

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-handler-configuration/spec.md`. Decisions in [research.md](research.md).

## Summary

`ExecutionPlanner` becomes the single orchestrator of handler configuration. `HandlerBuilder<T>` replaces `ExecutionPlanEntry`: `Add<T>()` returns it, and `IHandlerConfiguration<T>` classes receive it through `ApplyConfiguration` / `ApplyConfigurationsFromAssembly`. Each builder call records an **explicit value per item** on the built step; items not called stay unset. Providers resolve every item independently as builder > per-handler option > attribute > global default, warn when the builder overrides a per-handler option, and apply provider settings stored as named settings on the step (Hangfire: queue and retries as job filters).

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23

**Storage**: N/A

**Testing**: xUnit + NSubstitute; trace capture as in `IntervalRegistrationTests`

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library

**Performance Goals**: N/A (configuration time; filter lookup per job stays a dictionary read)

**Constraints**: core dependency-free; existing behavior kept except the confirmed `SetJobAutoRun` + plan change

**Scale/Scope**: tens of handlers

**Note**: no timeout extension — Hangfire 1.8 has no execution-timeout filter (research R-005); the README's `[Timeout]` example is removed.

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

- I. Dependency-free core: ✅ builder, configuration interface and named settings are core-only; the provider reads them.
- II. Processors isolated: ✅ Hangfire extensions live in `ExecutionFlow.Hangfire`.
- V. Minimal public surface / breaking changes: ⚠️ removes `ExecutionPlanEntry` (replaced by `HandlerBuilder<T>`), and replaces the `SetJobAutoRun` + plan error with "builder wins + warning". Both are 1.3.0 (unreleased) and confirmed by the maintainer in spec Clarifications (2026-11-02); no major bump.
- VII. Tested, traceable behavior: ✅ every AC names a test; the two tests asserting the removed error become precedence tests (SC-003).

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
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | New `IHandlerConfiguration<T>`, `HandlerBuilder`/`HandlerBuilder<T>`; `ExecutionPlanner.ApplyConfiguration`/`ApplyConfigurationsFromAssembly`; `Add` returns the builder; `ExecutionPlanStep` gains explicit values and settings; `ExecutionPlanEntry` removed | Yes, 1.3.0 unreleased (confirmed) |
| `ExecutionFlow.Hangfire` | Item resolution through the step; warnings; builder extensions `Queue`, `AutomaticRetry`; filters from settings | `SetJobAutoRun` + plan no longer throws (confirmed) |

```text
Src/ExecutionFlow/Abstractions/IHandlerConfiguration.cs, HandlerBuilder.cs, ExecutionPlanner.cs, ExecutionPlanStep.cs, ExecutionPlan.cs
Src/ExecutionFlow.Hangfire/HandlerBuilderExtensions.cs, HangfireOptions.cs, HangfireSetup.cs
Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs, HandlerJobFilterProvider.cs
Tests/ExecutionFlow.Tests/Abstractions/HandlerConfigurationTests.cs (+ migrated ExecutionPlannerTests)
Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs (+ migrated plan tests)
```

## Design

- **`HandlerBuilder`** (non-generic, used by `Add(Type)` and assembly scanning) and **`HandlerBuilder<T> : HandlerBuilder`** (fluent methods return `HandlerBuilder<T>`). Every call stores an explicit value: `Cron`/`Interval` (one clears the other), `Id`, `TimeZone` (null/empty → throws at call; unknown → throws at configuration, where the provider validates ids), `DisplayName`, `Enabled`, `DependsOn`/`RemoveDependency`/`ClearDependencies` (the attribute's `[DependsOn]` is loaded first, so remove/clear can drop it), `RunMode` (013), `NoOverlap`/`AllowOverlap`, and `HasSetting(string key, object value)` for provider extensions (R-004).
- **`ExecutionPlanner`**: `Add<T>()`/`Add(Type)` return the builder and throw when the handler is already configured; `ApplyConfiguration<T>(IHandlerConfiguration<T>)` = `config.Configure(Add<T>())`; `ApplyConfigurationsFromAssembly(Assembly)` finds non-abstract classes implementing `IHandlerConfiguration<>`, throws for a class without a parameterless constructor, creates and applies each.
- **`ExecutionPlanStep`** keeps today's members and gains the explicit values: `Cron`, `Interval`, `Id`, `TimeZone`, `ConfiguredEnabled` (bool?), `OverlapTimeout` (explicit value or `AllowOverlap`, R-002), `Settings` (read-only dictionary). `DisplayName`, `MinInterval`, `RunsOnOwnSchedule` keep their current resolution.
- **Enabled and availability** (R-003): the plan records its `MissingPrerequisite` policy. At configuration, the provider computes each step's enabled state (`ConfiguredEnabled` ?? per-handler option ?? global) and re-runs the availability pass with the recorded policy, so a prerequisite turned off by `SetJobAutoRun` still throws or cascades.
- **Item resolution in Hangfire**: `RecurringJobResolver` asks the step first for schedule, Id and time zone, then the option, then the registry (attribute), then the global default. `HangfireOptions.OnPlanRegistering` no longer throws on `SetJobAutoRun`; `HangfireSetup.OnConfigured` emits a trace warning for each step whose explicit value overrides a `SetJobAutoRun<T>` or `SetJobTimeZone<T>`, and for a time zone on an interval handler (012 rule).
- **Provider settings**: `HandlerBuilderExtensions.Queue(...)` and `.AutomaticRetry(...)` store a Hangfire filter instance under a known key; `HandlerJobFilterProvider` adds the step's filter instances and drops the handler's attribute filters of the same type, so the builder wins (FR-004).

## Complexity Tracking

| Item | Why | Simpler alternative rejected |
|---|---|---|
| Availability re-evaluated at configuration (R-003) | `SetJobAutoRun` may be set after `Build`, and must keep cascading as the plan's policy says | evaluating only at `Build` (would miss options set later) |
