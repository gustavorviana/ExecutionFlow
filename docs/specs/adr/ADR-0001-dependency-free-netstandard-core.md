# ADR-0001: Dependency-free core on netstandard2.0

Status: accepted (retroactive)
Date: 2026-09-30

## Context
Handler authors need types (`IHandler`, `FlowContext`, `IEventDispatcher`, events, attributes) that they can reference from domain or application projects without pulling in a job-processing backend. The library also has to be consumable from both .NET Framework and modern .NET hosts.

## Options considered
1. A single package that includes Hangfire: simpler to ship, but every project that defines a handler depends on Hangfire.
2. **A core with abstractions only on netstandard2.0, with the backend in separate packages**: clean dependency direction and the widest compatibility, but it rules out newer language/BCL features in the core.
3. Multi-target the core (netstandard2.0 + net8.0+): modern APIs where available, at the cost of `#if` branches and a larger test matrix.

## Decision
Option 2. `Src/ExecutionFlow/ExecutionFlow.csproj` targets `netstandard2.0` and has no `PackageReference` entries. All other packages in `Src/` also target `netstandard2.0`.

## Consequences
- Projects that define handlers only reference `ExecutionFlow`.
- No `Span`-heavy APIs, default interface members, `IAsyncEnumerable`, or file-scoped namespaces in `Src/`.
- Any new dependency in the core needs a new ADR.

## Related
- Principles: P-001, P-002
- NFRs: NFR-001, NFR-002
