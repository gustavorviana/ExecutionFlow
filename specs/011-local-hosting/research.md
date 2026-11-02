# Research: Local hosting

Decisions for [spec.md](spec.md). `R-001` was `DEC-001` of the old plan, promoted to ADR-0008 (absorbed here in full); `R-002` and `R-003` were `DEC-002`/`DEC-003`.

## R-001: A local hosting provider as a separate, multi-target package (was DEC-001 / ADR-0008, accepted 2026-09-30)

**Amended at the time**: the old constitution P-002 and NFR-001 (multi-targeting), now [constitution](../../.specify/memory/constitution.md) principle I; refined ADR-0007 (the second processor is an in-process executor), now constitution principle II.

**Context**: recurring handlers must also run without Hangfire: as a service or a console app on one machine, or once, on demand. That requires three things:
- the .NET Generic Host (`IHostedService`, `IHostApplicationLifetime`);
- a cron parser;
- a time source that tests can control.

None of these can go into the core without breaking the dependency-free core (old P-001, now constitution principle I). Execution concepts (plan, validation, order) must still stay shared with Hangfire, so the two providers can't drift apart.

**Alternatives considered**:
1. **Everything in the core.** It breaks the dependency-free core, or it forces us to write our own cron parser and leave hosting to the user.
2. **Execution engine in the core and a thin hosting adapter package.** The engine (gate, runner) is useless without the host, and it would add public surface to the core that Hangfire doesn't use.
3. **Core keeps declaration and validation, and a new provider package runs the handlers.** This mirrors `ExecutionFlow` + `ExecutionFlow.Hangfire`, and the market pattern (Quartz + `Quartz.Extensions.Hosting`, `Hangfire.Core` + `Hangfire.NetCore`).

**Decision**: option 3.
- The core (`ExecutionFlow`) holds `RecurringSchedule`, `[DependsOn]`, `ExecutionPlanner`/`ExecutionPlan` (validation and order). It stays `netstandard2.0` with no dependencies. (Correction: the original ADR named `ExecutionPlanBuilder`; the class in the code is `ExecutionPlanner`.)
- `ExecutionFlow.Hosting` holds only the execution inside the Generic Host: schedulers, dependency gate, concurrency limiter, run-once, and startup report.
  - It depends on `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0) and `Cronos`.
  - It targets `netstandard2.0;net8.0`. On `net8.0` it uses `TimeProvider` (optional, defaulting to `TimeProvider.System`). On `netstandard2.0` it uses an internal system-clock polyfill, with no `TimeProvider` and no `Microsoft.Bcl.TimeProvider`.
- There is a single package. The local executor only makes sense inside a Generic Host, so there is no "without DI" mode to split out.

**Consequences**:
- Old P-002 then read: every shipped library builds for `netstandard2.0`, and a package may add newer TFMs when it needs newer BCL types. The newer-TFM code sits behind `#if`, and the `netstandard2.0` build must stay functional. (This is now constitution principle I.)
- Tests with `FakeTimeProvider` only cover the `net8.0` asset. The `netstandard2.0` polyfill is thin, and it is covered by the build.
- The package isn't trimming-safe, because hosted services are closed with `MakeGenericType` (the same as the core `Scan`).

**Related**: constitution principles I, II, III (were P-001, P-002, P-004); FR-001..FR-009 of this feature; [010-execution-plan](../010-execution-plan/spec.md) FR-001..FR-004.

## R-002: Wait for prerequisites instead of skipping (was DEC-002)

- **Decision**: the local scheduler waits, with a periodic report. The semantics (a new generation of every prerequisite before each run) match Hangfire.
- **Rationale**: Hangfire can't hold a worker while it waits, so there a dependent run is skipped. The local scheduler can wait for free.
- **Alternatives considered**: skipping the run, as Hangfire does; not needed locally.
- **Consequences**: locally, a dependent runs as soon as its prerequisite completes, instead of at its next occurrence.

## R-003: `MakeGenericType` for hosted services (was DEC-003)

- **Decision**: register `RecurringHandlerHost<T>` through `MakeGenericType`.
- **Rationale**: handlers come from `Scan`, which already uses reflection.
- **Alternatives considered**: none recorded.
- **Consequences**: the package isn't trimming-safe, the same as the core `Scan`.
