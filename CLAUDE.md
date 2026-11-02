# ExecutionFlow

A lightweight C# layer over Hangfire for background jobs: handlers, lifecycle hooks, dispatching, recurring jobs, and built-in logging. It ships as 4 NuGet packages under `Src/`. The libraries target `netstandard2.0`, and the tests and examples use .NET 10.

## Build and test

```
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## Spec-Driven Development

This repo follows SDD with [GitHub Spec Kit](https://github.com/github/spec-kit). Read [.specify/memory/constitution.md](.specify/memory/constitution.md) before changing behavior.

- **Use the Spec Kit commands** for any work on specs, plans, or tasks: `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`. Don't use the `sdd` skill in this repo. Write everything in English.
- **Spec first.** Features live in `specs/NNN-short-name/` (`spec.md`, `plan.md`, `research.md`, `tasks.md`). Before changing behavior, update or create the spec. Pure refactors that don't change observable behavior don't need a spec change.
- **Amendments.** When a feature changes behavior specified by an earlier feature, its spec lists it under `**Amends**:`, and the earlier spec gets a one-line note under that requirement.
- **Never assume.** Write ambiguities as `[NEEDS CLARIFICATION: ...]` and ask, rather than guessing.
- **Stable IDs.** Never renumber `FR`/`AC`/`RN`/`SC`/`T` IDs. Mark removed items as deprecated.
- **Traceability.** Every acceptance scenario names the test that covers it. Tests are mandatory, even though the Spec Kit tasks template calls them optional. Commits and PRs reference the feature and the FR and T IDs they implement.
- **Decisions.** Feature decisions go in the feature's `research.md`. Project-wide decisions amend the constitution with `/speckit-constitution`.
- **Old IDs.** Commits from before the Spec Kit migration cite `REQ-n`/`TASK-n` of `docs/specs/<capability>/`. These map 1:1 to `FR-n`/`Tn` of `specs/NNN-<capability>/`.

## Conventions

- Block-scoped namespaces (no file-scoped), `_camelCase` private fields, and XML docs on public APIs.
- The core `ExecutionFlow` package has no external dependencies. Hangfire-specific code lives only in the `ExecutionFlow.Hangfire*` packages.
- Tests use xUnit and NSubstitute, named `Method_Expectation_WhenCondition`.
- The version is lockstep across all packages, in `Src/Directory.Build.props`. Releases are cut by pushing a `v*.*.*` tag.
