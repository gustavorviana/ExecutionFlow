# Constitution: ExecutionFlow

Status: draft
Scope: all packages under `Src/`, plus the tests and CI.

These are the non-negotiable principles for the project. A change that breaks one of them needs an ADR that amends this document first.

## Architectural principles

- **P-001: Dependency-free core.** The `ExecutionFlow` package references no NuGet packages. It holds only abstractions, options, and the handler registry. (`Src/ExecutionFlow/ExecutionFlow.csproj`; [ADR-0001](adr/ADR-0001-dependency-free-netstandard-core.md))
- **P-002: netstandard2.0 for all shipped libraries**, so the packages work on both .NET Framework 4.6.1+ and modern .NET. Language and BCL features that aren't available on netstandard2.0 are not used in `Src/`. ([ADR-0001](adr/ADR-0001-dependency-free-netstandard-core.md))
- **P-003: Hangfire is isolated behind the abstractions.** Only `ExecutionFlow.Hangfire*` packages reference Hangfire. Handler authors depend on `ExecutionFlow` types (`IHandler`, `FlowContext`, `IEventDispatcher`, `IExecutionManager`). ([ADR-0002](adr/ADR-0002-hangfire-as-execution-backend.md))
- **P-004: Optional integrations are separate packages.** Console output (`ExecutionFlow.Hangfire.Console`) and Microsoft DI (`ExecutionFlow.Hangfire.DependencyInjection`) are opt-in. `ExecutionFlow.Hangfire` works without a DI container.
- **P-005: Producer-only mode mutates no global state.** `BuildDispatcherOnly` / `AddExecutionFlowDispatcher` never touches `GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current`, or recurring jobs. ([ADR-0005](adr/ADR-0005-producer-only-mode.md))
- **P-006: Minimal public surface.** Settings users shouldn't touch are `internal`. Tests reach them through `InternalsVisibleTo`, not by making them public.
- **P-007: Dispatch doesn't block.** `Publish`/`Schedule` never wait on other publishes, except for a short, bounded lock around O(1) storage operations. That lock is taken only when deduplication is enabled and the event has a non-empty custom ID. ([custom-id-and-deduplication](custom-id-and-deduplication/spec.md))

## Testing strategy

- xUnit + NSubstitute, with coverlet for coverage.
- Test names follow `Method_Expectation_WhenCondition`.
- Each package has a matching test project under `Tests/` (the Console package is tested in `ExecutionFlow.Hangfire.Tests`).
- Every acceptance criterion in a spec is covered by at least one test, and the spec names that test.
- CI (`.github/workflows/dotnet.yml`) builds and tests every PR to `main`. A PR must be green to merge.

## Conventions

- English for code, XML docs, specs, ADRs, commits, and PRs.
- Block-scoped namespaces (no file-scoped namespaces), `_camelCase` private fields, and interfaces prefixed with `I`.
- XML doc comments on every public type and member.
- Folder layout per package: `Abstractions/`, `Infrastructure/`, `Attributes/`.
- Package versions are managed centrally in `Directory.Packages.props`.
- Commit subjects use conventional prefixes (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `ci:`) and reference the REQ/TASK IDs they implement.

## Versioning and release

- **Lockstep version** for all packages, in `Src/Directory.Build.props`. ([ADR-0003](adr/ADR-0003-lockstep-versioning.md))
- Semantic versioning: a breaking public API or behavior change bumps the major version, a new feature bumps the minor version, and a fix bumps the patch version.
- Releases are cut by pushing a `v*.*.*` tag, which publishes through NuGet Trusted Publishing. ([ADR-0004](adr/ADR-0004-nuget-trusted-publishing-on-tag.md))

## Mandatory non-functional requirements

- **NFR-001:** Every shipped library builds for `netstandard2.0`.
- **NFR-002:** `ExecutionFlow.csproj` has zero `PackageReference` entries.
- **NFR-003:** `dotnet build` and `dotnet test` pass on the .NET SDK version used in CI.
- **NFR-004:** No public API is removed or changed incompatibly without a major version bump.

## Change process

1. Update or create `docs/specs/<capability>/spec.md`.
2. Write `plan.md` for non-trivial changes, and promote architectural decisions to an ADR.
3. Write `tasks.md` when the change spans more than one PR or task.
4. Implement with tests that name the REQ/AC they cover.

## Open questions

- Hangfire is now pinned: `Hangfire.Core` and `Hangfire.AspNetCore` are fixed at 1.8.23, the same version as `Hangfire.SqlServer`. The floating `1.8.*` had resolved to 1.8.25 and conflicted with `Hangfire.SqlServer`, which requires exactly 1.8.23 (NU1107), so the solution didn't restore. 1.8.23 is therefore the minimum Hangfire version the packages declare.
- Resolved (2026-09-30, setup-and-configuration F-008): shipped libraries declare pinned lower bounds. `ExecutionFlow.Hangfire.DependencyInjection` requires `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions` >= 6.0.0 (via `VersionOverride`, while tests and examples use 10.x), and `Hangfire.Console` is pinned to 1.4.3.
- Resolved: the minimum supported Hangfire version is 1.8.23, the pinned version the packages declare and the tests run against. Older 1.8.x versions are not tested.
