# ADR-0003: Lockstep versioning across all packages

Status: accepted (retroactive)
Date: 2026-09-30

## Context
The 4 packages depend on each other through project references, so they are released together.

## Options considered
1. **One version for all packages**: simple, and consumers can tell which versions are compatible just by the number. The downside is that a package can get a version bump even when nothing in it changed.
2. Independent versions per package: more accurate semver, but it needs a compatibility matrix and a more complex release pipeline.

## Decision
Option 1. `<Version>` is defined once in `Src/Directory.Build.props`, and every package in `Src/*/*.csproj` is packed and pushed on each release.

## Consequences
- Consumers should use the same version of every ExecutionFlow package.
- A breaking change in any package bumps the major version of all of them.

## Related
- [ADR-0004](ADR-0004-nuget-trusted-publishing-on-tag.md)
- NFRs: NFR-004
