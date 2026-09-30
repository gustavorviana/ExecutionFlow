# ADR-0005: Producer-only (isolated) dispatcher

Status: accepted (retroactive)
Date: 2026-09-30

## Context
Some apps only publish jobs, and the jobs are run by another process. Sometimes those apps publish to a different storage than a Hangfire instance already running in the same process. A full `Build()` registers global filters, a filter provider, and recurring jobs (`Src/ExecutionFlow.Hangfire/HangfireSetup.cs:62-89`), which would interfere with that other instance.

## Options considered
1. Require a separate process for each Hangfire storage.
2. **A second build path that creates only a dispatcher**, bound to an explicit `JobStorage`, with no global side effects.

## Decision
Option 2:
- `HangfireSetup.BuildDispatcherOnly(JobStorage, IServiceProvider = null)` and `BuildDispatcherOnly(IBackgroundJobClient, JobStorage, IServiceProvider = null)` (`HangfireSetup.cs:98-128`) return an `IEventDispatcher`. They require a non-null client and storage, and don't touch `GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current`, or recurring jobs. When no provider is given, they use a private `FlowEngineJobActivator` with only `IJobIdGenerator` and `IHangfireJobName` registered.
- `AddExecutionFlowDispatcher(...)` (`Src/ExecutionFlow.Hangfire.DependencyInjection/ServiceCollectionExtensions.cs:89-123`) wraps it for DI.
- A `HangfireSetup` instance builds only once. Calling `Build` or `BuildDispatcherOnly` a second time throws (`ThrowIfBuilt`).

## Consequences
- The producer usually doesn't know the handlers, so dashboard names and similar features fall back to event type names ([ADR-0006](ADR-0006-job-naming-fallback.md)).
- It returns `IEventDispatcher`, not `IHangfireDispatcher`, so recurring triggers aren't available in producer-only mode.

## Related
- Principles: P-005
- Capabilities: `setup-and-configuration`, `dispatching`
