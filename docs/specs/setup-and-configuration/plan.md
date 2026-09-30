# Plan: Setup and configuration (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–6)
Status: approved

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `ExecutionFlowOptions` property setters throw after `Configure` | Behavior |
| `ExecutionFlow.Hangfire` | `HangfireSetup`: own activator per setup, the last full build replaces the previous global filters, `ExecutionManager` property (both modes). `HangfireOptions` setters throw after `Configure` | Behavior |
| `ExecutionFlow.Hangfire.DependencyInjection` | `StartExecutionFlow()`, both-modes guard, `IExecutionManager` in producer-only, lower dependency floor | **Yes**: registering both modes now throws |

## Design
- **Activator ownership (F-001):** `HangfireSetup._activator`. `ConfigureActivator()` creates it if needed and installs it as `JobActivator.Current`. `Build()` without a provider uses `_activator`, creating it if needed. `JobActivator.Current` is never read.
- **Last full build wins (F-002):** a static `_activeFullSetup`, guarded by a static lock. `Build()` removes the previous active setup's `HangfireStateFilter`, `DeduplicationCleanupFilter` and `HandlerJobFilterProvider` instances, which each setup keeps references to, then registers its own and becomes active.
- **Locked properties (F-003):** property setters on `ExecutionFlowOptions` and `HangfireOptions` call `ThrowIfLocked()`.
- **Startup (F-004):** `IServiceProvider.StartExecutionFlow()` resolves `IEventDispatcher`, which triggers `Build()` in full mode. The hosted service is renamed `ExecutionFlowStartupService` and calls the same method.
- **Both modes (F-005):** each extension registers a private marker type. Registering one after the other throws `InvalidOperationException` naming the call to keep.
- **Producer-only manager (F-006):** `HangfireSetup.ExecutionManager` is set by `CreateDispatcher` in both modes, and registered in the built-in activator. DI: `AddExecutionFlowDispatcher` registers `IExecutionManager` bound to the same storage.
- **Dependencies (F-008):** the DI project uses `VersionOverride` for `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions` at the lowest version it compiles against. `Hangfire.Console` is pinned to the version resolved today.

## Decisions
### DEC-001: The last full build wins (option A)
- Chosen by the maintainer over "throw on the second build" and "scope filters per storage". Scoping is recorded in ADR-0007.

## Versioning impact
1.2.0.
