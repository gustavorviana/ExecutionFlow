<!--
Sync Impact Report
- Version change: 1.0.0 → 1.1.0 (2026-11-02)
- Modified principle: V. Minimal public surface — adds "Breaking changes need the maintainer's explicit
  confirmation": recorded in the spec's Clarifications, plus a major bump; CRITICAL in /speckit-analyze;
  /speckit-implement stops and asks.
- Templates: unchanged. Follow-up: none (existing specs record no unconfirmed breaking change pending).

Previous report (1.0.0):
- Version change: (template) → 1.0.0
- Origin: migrated from docs/specs/constitution.md (P-001..P-007, NFR-001..NFR-004) and the global ADRs
  ADR-0001, ADR-0002, ADR-0003, ADR-0004 and ADR-0007, which are absorbed as principles below.
- Feature-level ADRs moved to the research.md of their feature:
  ADR-0005 → specs/002-setup-and-configuration, ADR-0006 → specs/008-job-naming-and-dashboard,
  ADR-0008 → specs/011-local-hosting, ADR-0009 → specs/010-execution-plan.
- Templates: unchanged (.specify/templates/*). This constitution overrides them where they differ
  (tests are mandatory; IDs are stable; acceptance scenarios name their test).
-->

# ExecutionFlow Constitution

Scope: every package under `Src/`, plus the tests, the examples and CI. These principles are non-negotiable. A change that breaks one of them amends this document first (see Governance).

## Core Principles

### I. Dependency-free core on netstandard2.0

The `ExecutionFlow` package references no NuGet packages (`Src/ExecutionFlow/ExecutionFlow.csproj` has zero `PackageReference` entries). It holds only abstractions, options, attributes, the handler registry and the execution plan.

Every shipped library builds for `netstandard2.0`, so the packages work on .NET Framework 4.6.1+ and modern .NET. A package may add newer target frameworks when it needs newer BCL types (e.g. `TimeProvider`); that code sits behind `#if`, and the `netstandard2.0` build stays functional.

*Rationale (was ADR-0001):* handler authors reference only `ExecutionFlow` from domain or application projects, without pulling in a job backend. A single package with Hangfire was rejected because every project that defines a handler would depend on Hangfire. Consequence: no `Span`-heavy APIs, default interface members, `IAsyncEnumerable` or file-scoped namespaces in `Src/`, and any new core dependency needs an amendment.

### II. Processors are isolated behind the abstractions

Only `ExecutionFlow.Hangfire*` packages reference Hangfire, and only `ExecutionFlow.Hosting` references the Generic Host and Cronos. Handler authors depend on `ExecutionFlow` types (`IHandler`, `FlowContext`, `IEventDispatcher`, `IExecutionManager`, `ExecutionPlanner`).

Hangfire is **one** processor, not the only one. New decisions must not assume a single processor per process. The "one active full setup per process" limit belongs to Hangfire (it registers into process-wide statics: `GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current`) and must not become an ExecutionFlow-wide rule.

*Rationale (was ADR-0002 and ADR-0007):* Hangfire was chosen for persistent fire-and-forget, delayed and recurring execution, retries, state tracking and a dashboard; Quartz.NET (no dashboard) and a lowest-common-denominator multi-backend contract were rejected. The intent to support other processors is recorded, and processor-neutral concepts that still live in `ExecutionFlow.Hangfire` are to move to the core in a dedicated cycle. Inventory as of 2026-09-30:

| Concept | Today | Likely target |
|---|---|---|
| Hook registration (`AddStateHandler`) and `HookErrorHandler` | `HangfireOptions` | `ExecutionFlowOptions` |
| Recurring on/off (`SetJobAutoRun`, `GlobalRecurringAutoRun`), time zones (`RecurringTimeZone`, `SetJobTimeZone`) | `HangfireOptions` | `ExecutionFlowOptions` |
| `DisableRecurringRetries`, `RetryUnregisteredEventJobs` | `HangfireOptions` | core, if the other processor supports retries |
| `IRecurringTrigger` | `ExecutionFlow.Hangfire` namespace | core abstractions |
| `JobDisplayNameResolver` rules (take a Hangfire `Job`) | `ExecutionFlow.Hangfire` | core, taking processor-neutral job facts |
| Deduplication lock options | `HangfireOptions` | stay per processor (storage-specific) |

That migration is a breaking change and belongs in a major version or a planned minor with release notes. Hangfire 1.8 accepts a filter provider and an activator per server and per client, which would later allow several full Hangfire setups in one process.

### III. Optional integrations are separate packages

Console output (`ExecutionFlow.Hangfire.Console`), Microsoft DI (`ExecutionFlow.Hangfire.DependencyInjection`) and local execution (`ExecutionFlow.Hosting`) are opt-in packages. `ExecutionFlow.Hangfire` works without a DI container.

### IV. Producer-only mode mutates no global state

`BuildDispatcherOnly` / `AddExecutionFlowDispatcher` never touch `GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current` or recurring jobs. (Design in `specs/002-setup-and-configuration/research.md`.)

### V. Minimal public surface

Settings users shouldn't touch are `internal`. Tests reach them through `InternalsVisibleTo`, not by making them public.

**Breaking changes need the maintainer's explicit confirmation (NON-NEGOTIABLE).** No public API is removed or changed incompatibly (types, members, signatures, namespaces, attribute usage), and no documented behavior is changed incompatibly (e.g. a default value, an exception that is or isn't thrown, an order or a state), without both:

1. the maintainer's explicit confirmation, recorded in the feature's `spec.md` under *Clarifications* with the date and what breaks; and
2. a major version bump.

Compatible changes (new types, new members, new optional settings whose default keeps the current behavior) don't need this confirmation; they follow the normal versioning rules. A spec, plan or task that introduces a breaking change without that record is a CRITICAL finding in `/speckit-analyze`, and `/speckit-implement` stops and asks the maintainer instead of proceeding. When in doubt whether a change breaks, treat it as breaking and ask.

### VI. Dispatch doesn't block

`Publish`/`Schedule` never wait on other publishes, except for a short, bounded lock around O(1) storage operations. That lock is taken only when deduplication is enabled and the event has a non-empty custom ID. (See `specs/004-custom-id-and-deduplication`.)

### VII. Tested, traceable behavior (NON-NEGOTIABLE)

- Tests are **mandatory** for every behavior change. This overrides the "tests are optional" note in `.specify/templates/tasks-template.md`.
- Every acceptance scenario has a stable ID `AC-<FR>.<n>` (e.g. `AC-003.1` belongs to `FR-003`) and names the test that covers it: `Test: ClassName.Method_Expectation_WhenCondition`.
- IDs are **stable**: `FR`, `AC`, `SC`, `T`, and principle numbers are never renumbered or reused. A removed item stays in place, marked `~~deprecated~~` with the reason.
- Ambiguities are written as `[NEEDS CLARIFICATION: ...]` and asked, never guessed.

## Technical Constraints

- **Stack:** C#; libraries on `netstandard2.0` (Principle I); tests and examples on .NET 10. Package versions are managed centrally in `Directory.Packages.props`.
- **Hangfire:** `Hangfire.Core`, `Hangfire.AspNetCore` and `Hangfire.SqlServer` are pinned to 1.8.23, which is the minimum version the packages declare and the version the tests run against. (A floating `1.8.*` resolved to 1.8.25 and conflicted with `Hangfire.SqlServer`, NU1107.)
- **Lower bounds:** `ExecutionFlow.Hangfire.DependencyInjection` requires `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions` >= 6.0.0 (via `VersionOverride`); `Hangfire.Console` is pinned to 1.4.3.
- **Code conventions:** English for code, XML docs, specs, commits and PRs. Block-scoped namespaces (no file-scoped), `_camelCase` private fields, interfaces prefixed with `I`, XML doc comments on every public type and member. Folder layout per package: `Abstractions/`, `Infrastructure/`, `Attributes/`.
- **Testing:** xUnit + NSubstitute, coverlet for coverage, test names `Method_Expectation_WhenCondition`. Each package has a matching test project under `Tests/` (the Console package is tested in `ExecutionFlow.Hangfire.Tests`).
- **Build gate:** `dotnet build` and `dotnet test` pass on the .NET SDK used in CI (`.github/workflows/dotnet.yml`), which runs on every PR to `main`. A PR must be green to merge.

## Versioning and Release

- **Lockstep version** for all packages, defined once in `Src/Directory.Build.props`; every `Src/*/*.csproj` is packed and pushed on each release. Consumers use the same version of every ExecutionFlow package, and a breaking change in any package bumps the major version of all of them. *(was ADR-0003: independent versions were rejected because they need a compatibility matrix.)*
- **Semantic versioning:** breaking public API or behavior → major; new feature → minor; fix → patch.
- **Tag-triggered publishing:** pushing a `v*.*.*` tag runs `.github/workflows/nuget-publish.yml`, which takes the version from the tag, builds, tests and packs each package with `-p:Version=<tag>`, exchanges the GitHub OIDC token for a temporary key (NuGet Trusted Publishing, `NuGet/login@v1`) and pushes with `--skip-duplicate`. No API key secret is stored; re-running a tag is a no-op. *(was ADR-0004.)*
- [NEEDS CLARIFICATION: the workflow header says it "validates tag == csproj version", but no step does. The tag overrides `Src/Directory.Build.props`, so a forgotten bump still publishes and `main` keeps a stale number. Should the workflow fail on a mismatch, or should the props file stop holding the version?]

## Development Workflow

- Behavior changes follow the Spec Kit flow: `/speckit-specify` → `/speckit-clarify` (when there are ambiguities) → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`. Pure refactors that don't change observable behavior don't need a spec.
- Features live in `specs/NNN-short-name/`. Branches keep the `feat/…`, `fix/…`, `chore/…` convention; Spec Kit tracks the active feature in `.specify/feature.json`.
- **Amendments between features.** When a feature changes behavior specified by an earlier feature, its `spec.md` lists it under `**Amends**:` (e.g. `Amends: 005-recurring-jobs FR-009`), and the earlier spec gets a one-line note under that requirement pointing to the amending feature. The earlier spec is not otherwise rewritten.
- Design decisions of a feature (options considered, choice, consequences) go in that feature's `research.md`. Decisions that bind the whole project amend this constitution.
- Commit subjects use conventional prefixes (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `ci:`, `chore:`) and reference the feature and the FR/T IDs they implement (e.g. `feat(011): dependency gate (FR-005, T002)`).
- **History before the migration:** commits before the Spec Kit migration reference `REQ-n`, `AC-n.m` and `TASK-n` of `docs/specs/<capability>/`. They map 1:1 to `FR-n`, `AC-n.m` and `Tn` of the corresponding `specs/NNN-<capability>/`, with the same numbers.

## Governance

This constitution supersedes other practices in the repository. Amendments are made with `/speckit-constitution`, update the Sync Impact Report at the top, and follow semantic versioning: removing or redefining a principle → major; adding a principle or section → minor; wording → patch. Every PR review checks compliance with the principles above, and `/speckit-analyze` treats a violation as CRITICAL.

**Version**: 1.1.0 | **Ratified**: 2026-09-30 | **Last Amended**: 2026-11-02
