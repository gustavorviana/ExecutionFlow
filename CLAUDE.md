# ExecutionFlow

A lightweight C# layer over Hangfire for background jobs: handlers, lifecycle hooks, dispatching, recurring jobs, and built-in logging. It ships as 4 NuGet packages under `Src/`. The libraries target `netstandard2.0`, and the tests and examples use .NET 10.

## Build and test

```
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## Spec-Driven Development

This repo follows SDD. Read [docs/specs/README.md](docs/specs/README.md) and [docs/specs/constitution.md](docs/specs/constitution.md) before changing behavior.

- **Spec first.** Before changing behavior, find the capability in `docs/specs/<capability>/spec.md` and update the spec before the code. If no spec covers the change, write one first. Pure refactors that don't change observable behavior don't need a spec change.
- **Use the `sdd` skill** for any work on specs, plans, tasks, or reviews. Use the templates in `docs/specs/_templates/`, which override the skill's defaults for this repo. Write everything in English.
- **Never assume.** Write ambiguities as `[NEEDS CLARIFICATION: ...]` and ask, rather than guessing.
- **Stable IDs.** Never renumber `REQ`/`AC`/`RN`/`TASK`/`ADR` IDs. Mark removed items as deprecated.
- **Traceability.** Every acceptance criterion names the test that covers it. Commits and PRs reference the REQ and TASK IDs they implement.
- **Architectural decisions** go in `docs/specs/adr/` as `ADR-NNNN-title.md`.

## Conventions

- Block-scoped namespaces (no file-scoped), `_camelCase` private fields, and XML docs on public APIs.
- The core `ExecutionFlow` package has no external dependencies. Hangfire-specific code lives only in the `ExecutionFlow.Hangfire*` packages.
- Tests use xUnit and NSubstitute, named `Method_Expectation_WhenCondition`.
- The version is lockstep across all packages, in `Src/Directory.Build.props`. Releases are cut by pushing a `v*.*.*` tag.
