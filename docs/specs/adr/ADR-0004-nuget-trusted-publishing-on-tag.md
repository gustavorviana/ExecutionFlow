# ADR-0004: Tag-triggered publishing with NuGet Trusted Publishing

Status: accepted (retroactive)
Date: 2026-09-30

## Context
Publishing needs to be reproducible and gated by tests, and it shouldn't depend on a long-lived API key stored in the repo.

## Options considered
1. Manual `dotnet nuget push` from a developer machine.
2. CI with a long-lived NuGet API key secret.
3. **CI triggered by a `v*.*.*` tag, authenticated through NuGet Trusted Publishing (GitHub OIDC)**: each release gets a short-lived key, and no API key secret is stored.

## Decision
Option 3. `.github/workflows/nuget-publish.yml` runs on `v*.*.*` tags and does the following:
1. Takes the version from the tag.
2. Builds, tests, and packs each `Src/*/*.csproj` with `-p:Version=<tag>`.
3. Exchanges the OIDC token for a temporary key (`NuGet/login@v1`) and pushes with `--skip-duplicate`.

## Consequences
- Releasing only takes a tag. The only secret it needs is `NUGET_USER`, the profile name, which isn't a credential.
- **Inconsistency (finding):** the header of the workflow says it "validates tag == csproj version", but no step does that. The tag overrides `Src/Directory.Build.props`. If you forget to bump the props file, a tag still publishes, and `main` then keeps a stale version number. [NEEDS CLARIFICATION: should the workflow fail when the tag doesn't match `<Version>`, or should the props file stop holding the version?]
- `--skip-duplicate` makes re-running a tag a no-op, so a partial failure is recovered by re-running the job.

## Related
- [ADR-0003](ADR-0003-lockstep-versioning.md)
