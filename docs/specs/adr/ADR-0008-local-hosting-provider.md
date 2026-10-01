# ADR-0008: A local hosting provider as a separate, multi-target package

Status: accepted
Date: 2026-09-30
Amends: [constitution](../constitution.md) P-002 and NFR-001 (multi-targeting); refines [ADR-0007](ADR-0007-multiple-processors.md) (the second processor is an in-process executor)

## Context
Recurring handlers must also run without Hangfire: as a service or a console app on one machine, or once, on demand. That requires three things:
- the .NET Generic Host (`IHostedService`, `IHostApplicationLifetime`);
- a cron parser;
- a time source that tests can control.

None of these can go into the core without breaking P-001, the dependency-free core. Execution concepts (plan, validation, order) must still stay shared with Hangfire, so the two providers can't drift apart.

## Options considered
1. **Everything in the core.** It breaks P-001, or it forces us to write our own cron parser and leave hosting to the user.
2. **Execution engine in the core and a thin hosting adapter package.** The engine (gate, runner) is useless without the host, and it would add public surface to the core that Hangfire doesn't use.
3. **Core keeps declaration and validation, and a new provider package runs the handlers.** This mirrors `ExecutionFlow` + `ExecutionFlow.Hangfire`, and the market pattern (Quartz + `Quartz.Extensions.Hosting`, `Hangfire.Core` + `Hangfire.NetCore`).

## Decision
Option 3.
- The core (`ExecutionFlow`) holds `RecurringSchedule`, `[DependsOn]`, `ExecutionPlanBuilder`/`ExecutionPlan` (validation and order). It stays `netstandard2.0` with no dependencies.
- `ExecutionFlow.Hosting` holds only the execution inside the Generic Host: schedulers, dependency gate, concurrency limiter, run-once, and startup report.
  - It depends on `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0) and `Cronos`.
  - It targets `netstandard2.0;net8.0`. On `net8.0` it uses `TimeProvider` (optional, defaulting to `TimeProvider.System`). On `netstandard2.0` it uses an internal system-clock polyfill, with no `TimeProvider` and no `Microsoft.Bcl.TimeProvider`.
- There is a single package. The local executor only makes sense inside a Generic Host, so there is no "without DI" mode to split out.

## Consequences
- P-002 now reads: every shipped library builds for `netstandard2.0`, and a package may add newer TFMs when it needs newer BCL types. The newer-TFM code sits behind `#if`, and the `netstandard2.0` build must stay functional.
- Tests with `FakeTimeProvider` only cover the `net8.0` asset. The `netstandard2.0` polyfill is thin, and it is covered by the build.
- The package isn't trimming-safe, because hosted services are closed with `MakeGenericType` (the same as the core `Scan`).

## Related
- Principles: P-001, P-002, P-004
- Requirements: local-hosting/REQ-001..REQ-009, execution-plan/REQ-001..REQ-004
