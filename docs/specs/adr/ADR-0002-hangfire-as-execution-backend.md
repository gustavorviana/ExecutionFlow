# ADR-0002: Hangfire as the execution backend

Status: accepted (retroactive)
Date: 2026-09-30

## Context
ExecutionFlow needs persistent fire-and-forget, delayed, and recurring execution, plus retries, state tracking, and a dashboard. Building those features is out of scope for the library.

## Options considered
1. **Hangfire**: mature, storage-agnostic, with a dashboard, and extensible through job filters and a job activator.
2. Quartz.NET: strong scheduling, but no built-in dashboard, and a different model for fire-and-forget.
3. Multiple pluggable backends: flexible, but every capability would need a lowest-common-denominator contract.

## Decision
Option 1, and Hangfire is the only backend (as of v1.2.0; see [ADR-0007](ADR-0007-multiple-processors.md) for the intent to support other processors). It plugs in through `HangfireSetup` (`Src/ExecutionFlow.Hangfire/HangfireSetup.cs`), which installs global filters (`HangfireStateFilter`, `DeduplicationCleanupFilter`; `HangfireAutoRunFilter` until 1.2.0), a `HandlerJobFilterProvider`, and a `FlowEngineJobActivator`.

The abstractions in `ExecutionFlow` (for example `IEventDispatcher`, `IExecutionManager`, `JobState`) are shaped to be backend-neutral. ~~No second backend is planned.~~ Superseded by [ADR-0007](ADR-0007-multiple-processors.md).

## Consequences
- Capabilities such as custom IDs, deduplication, the execution manager, and recurring control are defined by Hangfire's storage and state model.
- In full mode, `Build()` mutates process-wide Hangfire state (`GlobalJobFilters`, `JobFilterProviders`). That is why producer-only mode exists ([ADR-0005](ADR-0005-producer-only-mode.md)).
- Hangfire version compatibility matters to consumers (see the open question in the constitution).

## Related
- Principles: P-003
