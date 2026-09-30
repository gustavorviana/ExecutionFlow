# ADR-0007: Keep the door open for processors other than Hangfire

Status: proposed
Date: 2026-09-30
Supersedes: the "No second backend is planned" statement in [ADR-0002](ADR-0002-hangfire-as-execution-backend.md)

## Context
The maintainer intends to support at least one processor besides Hangfire. What kind (another job scheduler such as Quartz.NET, a message broker such as RabbitMQ or Azure Service Bus, or an in-memory executor) and whether two processors must run in the same process are still open.

The core (`ExecutionFlow`) already has no Hangfire dependency (P-001), and `ExecutionFlowSetup<TOptions>` was designed as a base for a processor-specific setup (`HangfireSetup : ExecutionFlowSetup<HangfireOptions>`). However, several processor-neutral concepts ended up in the Hangfire package.

## Decision
1. Treat Hangfire as **one** processor, not the only one. New decisions must not assume a single processor per process. For example, "one active setup per process" is scoped to Hangfire ([setup-and-configuration](../setup-and-configuration/spec.md), to-be item 1).
2. Don't migrate now. After v1.2.0, run a dedicated SDD cycle that moves the processor-neutral concepts listed below to the core, once the second processor's kind is known.

## Concepts to migrate (inventory, 2026-09-30)
| Concept | Today | Likely target |
|---|---|---|
| Hook registration (`AddStateHandler`) and `HookErrorHandler` | `HangfireOptions` | `ExecutionFlowOptions` |
| Recurring on/off (`SetJobAutoRun`, `GlobalRecurringAutoRun`), time zones (`RecurringTimeZone`, `SetJobTimeZone`) | `HangfireOptions` | `ExecutionFlowOptions` |
| `DisableRecurringRetries`, `RetryUnregisteredEventJobs` | `HangfireOptions` | core, if the new processor supports retries |
| `IRecurringTrigger` | `ExecutionFlow.Hangfire` namespace | core abstractions |
| `JobDisplayNameResolver` rules (take a Hangfire `Job`) | `ExecutionFlow.Hangfire` | core, taking processor-neutral job facts |
| Deduplication lock options (`DeduplicationLockTimeout`, `CreateOnDeduplicationLockTimeout`) | `HangfireOptions` | stay per processor (the mechanism is storage-specific) |

## Several setups of the same processor
The limit of "one active full setup per process" (the last `Build()` wins) is **Hangfire's**, because ExecutionFlow registers into Hangfire's process-wide statics (`GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current`). It must not become an ExecutionFlow-wide rule: Hangfire + X and X + X (with different configurations) are expected combinations.

Possible evolution for Hangfire itself: Hangfire 1.8 accepts a `FilterProvider` and an `Activator` per server (`BackgroundJobServerOptions`), and a filter provider per client (`BackgroundJobClient(JobStorage, IJobFilterProvider)`) and per `RecurringJobManager`. Handing those to the setup instead of using the globals would allow several full Hangfire setups, e.g. on different storages, in one process.

## Consequences
- Until the migration, adding a processor means either depending on `ExecutionFlow.Hangfire` types or duplicating those concepts.
- The migration will be a breaking change (namespaces and options move), best done in a major version or a planned minor with release notes.

## Related
- [ADR-0002](ADR-0002-hangfire-as-execution-backend.md)
- Principles: P-001, P-003
