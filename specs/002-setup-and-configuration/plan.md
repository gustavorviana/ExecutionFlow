# Implementation Plan: Setup and configuration

**Branch**: `main` (brownfield capability) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/setup-and-configuration/plan.md` (status: approved; covers the spec's to-be changes 1–6). Decisions and findings are in [research.md](research.md).

## Summary

Make setups own their activator, let the last full build replace the previous global filters, lock option setters after `Configure`, add `StartExecutionFlow()`, forbid both DI modes in one container, expose `IExecutionManager` in producer-only, and lower the DI dependency floor.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: Hangfire 1.8.23 (Hangfire packages); `Microsoft.Extensions.*.Abstractions` >= 6.0.0 (DI package)

**Storage**: Hangfire `JobStorage`

**Testing**: xUnit + NSubstitute (`HangfireSetupTests`, `HangfireSetupGlobalStateTests`, `HangfireSetupGlobalFiltersTests`, `ServiceCollectionExtensionsTests`, `FlowEngineJobActivatorTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: N/A (startup only)

**Constraints**: producer-only mode mutates no global state (principle IV)

**Scale/Scope**: one full Hangfire setup per process; unlimited producer-only setups

## Constitution Check

- I. Dependency-free core: ✅ only option-locking changes in the core.
- II. Processors isolated: ✅ the "one full setup" limit is scoped to Hangfire (RN-002).
- III. Optional integrations: ✅ DI changes stay in `ExecutionFlow.Hangfire.DependencyInjection`.
- IV. Producer-only mutates no global state: ✅ `IExecutionManager` in producer-only adds no global registration.
- V. Minimal public surface: ⚠️ behavior changes in 1.2.0 (see Complexity Tracking).
- VII. Tested, traceable behavior: ✅ every AC names its tests (F-007 open for the scan path).

## Project Structure

### Documentation (this feature)

```text
specs/002-setup-and-configuration/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `ExecutionFlowOptions` property setters throw after `Configure` | Behavior |
| `ExecutionFlow.Hangfire` | `HangfireSetup`: own activator per setup, the last full build replaces the previous global filters, `ExecutionManager` property (both modes). `HangfireOptions` setters throw after `Configure` | Behavior |
| `ExecutionFlow.Hangfire.DependencyInjection` | `StartExecutionFlow()`, both-modes guard, `IExecutionManager` in producer-only, lower dependency floor | **Yes**: registering both modes now throws |

```text
Src/ExecutionFlow/ (ExecutionFlowSetup, ExecutionFlowOptions)
Src/ExecutionFlow.Hangfire/ (HangfireSetup.cs, HangfireOptions, FlowEngineJobActivator)
Src/ExecutionFlow.Hangfire.DependencyInjection/ServiceCollectionExtensions.cs
```

## Design

- **Activator ownership (F-001):** `HangfireSetup._activator`. `ConfigureActivator()` creates it if needed and installs it as `JobActivator.Current`. `Build()` without a provider uses `_activator`, creating it if needed. `JobActivator.Current` is never read.
- **Last full build wins (F-002):** a static `_activeFullSetup`, guarded by a static lock. `Build()` removes the previous active setup's `HangfireStateFilter`, `DeduplicationCleanupFilter` and `HandlerJobFilterProvider` instances, which each setup keeps references to, then registers its own and becomes active.
- **Locked properties (F-003):** property setters on `ExecutionFlowOptions` and `HangfireOptions` call `ThrowIfLocked()`.
- **Startup (F-004):** `IServiceProvider.StartExecutionFlow()` resolves `IEventDispatcher`, which triggers `Build()` in full mode. The hosted service is renamed `ExecutionFlowStartupService` and calls the same method.
- **Both modes (F-005):** each extension registers a private marker type. Registering one after the other throws `InvalidOperationException` naming the call to keep.
- **Producer-only manager (F-006):** `HangfireSetup.ExecutionManager` is set by `CreateDispatcher` in both modes, and registered in the built-in activator. DI: `AddExecutionFlowDispatcher` registers `IExecutionManager` bound to the same storage.
- **Dependencies (F-008):** the DI project uses `VersionOverride` for `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions` at the lowest version it compiles against. `Hangfire.Console` is pinned to the version resolved today.

Versioning impact: 1.2.0 (see constitution "Versioning and Release").

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
| --------- | ---------- | ------------------------------------ |
| Behavior changes (setters throw, both DI modes throw) shipped in a minor (1.2.0) | They fix silent misconfiguration (F-003, F-005) | Keeping the old behavior left silently ignored options and publishing to the wrong storage |
