# Research: Custom ID and deduplication

Decisions for [spec.md](spec.md). `R-001`..`R-004` were `DEC-001`..`DEC-004` of the old plan. The brownfield findings follow.

## R-001: Reservation key instead of a Monitoring API scan (was DEC-001)

- **Context**: F-003, F-005. The Monitoring API is for the dashboard, is O(n), and isn't atomic.
- **Decision**: a hash key per custom ID under a short lock per ID, following the `sidekiq-unique-jobs` model.
- **Rationale / consequences**: publishing with deduplication costs O(1) storage operations plus the lock. Jobs created before 1.2.0 have no key, so **deduplication doesn't see them** (lookups through the execution manager still do, through the scan). A crash between creating the job and writing the key can allow one duplicate.
- **Alternatives considered**: keep scanning the Monitoring API (O(active jobs), not atomic).

## R-002: Lock timeout (was DEC-002)

- **Decision**: `DeduplicationLockTimeout` defaults to 1s. On timeout, `DistributedLockTimeoutException` is thrown. `CreateOnDeduplicationLockTimeout = true` creates the job without a reservation and logs a warning.
- **Rationale**: a publish never waits unboundedly (constitution principle VI), and failing is the safe default.
- **Alternatives considered**: none recorded.

## R-003: Move the enum to `ExecutionFlow.Abstractions` in 1.2.0 (was DEC-003)

- **Context**: the attribute lives on event classes, which reference only the core (was P-003, now [constitution](../../.specify/memory/constitution.md) principle II).
- **Decision**: move it and change the namespace, accepting a source break in a minor version. This is the maintainer's decision, which knowingly deviates from semver.
- **Consequences**: listed as a breaking change in the release notes.
- **Alternatives considered**: wait for 2.0 (not chosen).

## R-004: The attribute reads the runtime class (was DEC-004)

- **Decision**: `[Deduplication]` is read from `@event.GetType()`, because the attribute is declared on the class the user wrote. Routing still uses `TEvent` ([003-dispatching](../003-dispatching/spec.md) RN-001).
- **Alternatives considered**: read it from `TEvent` (rejected: misses the attribute when publishing through a base type).

## Findings (brownfield, was findings.md)

- **F-001** possible bug: deduplication only considers Processing and Enqueued jobs (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireExecutionManager.cs`, `IsRunning`/`IsPending`). Delayed or scheduled publishes, and failed jobs waiting for an automatic retry (Hangfire moves them to Scheduled), aren't seen. So publishing the same custom ID again creates a duplicate.
  - Related: RN-003, AC-003.2
  - Resolution: fixed (FR-004, FR-007, RN-003)

- **F-002** rule: `ReplaceExisting` on a Processing job only marks it Deleted. The running handler keeps going unless it observes the `CancellationToken`, so the old and new job can run at the same time.
  - Related: AC-005.2
  - Resolution: accepted as rule (AC-005.2), documented in the README

- **F-003** limitation: deduplication checks and then creates, without any atomic primitive (`HangfireDispatcher.CheckDeduplication` followed by `CreateJob`). Two producers publishing the same custom ID at the same time both create a job.
  - Related: RN-005
  - Resolution: fixed (FR-007, RN-005); the crash window is documented

- **F-004** bug (regression in unpublished v1.2.0): `FlowContext.SetCustomId(null)` stored the JSON literal `null`, which `JobParameters.Decode` returned as the string `"null"`. So the job looked like it had the custom ID "null".
  - Related: AC-002.3
  - Resolution: fixed. `Decode` maps the literal `null` to no custom ID. Tests: `JobParametersTests.WriteCustomId_Null_ReadsBackAsNull`, `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ClearsCustomId_WhenHandlerSetsNull`

- **F-005** design debt (not market-grade): lookups by custom ID and deduplication page through the Monitoring API, which Hangfire provides for the dashboard, and read one job parameter per active job (`InfraUtils.ReadAll`, page size 10). That's O(active jobs) storage calls per publish with deduplication on. Tools that guarantee uniqueness (Sidekiq unique jobs, Temporal workflow IDs, Service Bus and SQS FIFO duplicate detection) use an atomic key lookup instead.
  - Related: RN-006, SC-001 (was NFR-001)
  - Resolution: fixed for publishing (FR-007); execution manager lookups use the key first and fall back to the scan (RN-006)

- **F-006** untested: nothing tests that `SetCustomId(id)` with a non-null value overwrites the stored job parameter, and that lookups see the new value (RN-004). `ContextTests.SetCustomId_Stores_Value_Readable_Via_CustomId` only checks the in-memory `FlowContext.CustomId`.
  - Related: AC-002.2, RN-004
  - Resolution: open

- **F-007** untested: with several jobs sharing one custom ID, nothing tests which one `Cancel`/`Retry` acts on (AC-003.4). `Publish_ReplaceExisting_CancelsAndEnqueuesNew` doesn't check that the old job was deleted (AC-005.1).
  - Related: AC-003.4, AC-005.1
  - Resolution: partly fixed. The deletion is now asserted (`Publish_ReplaceExisting_CancelsAndEnqueuesNew`, `Publish_ReplaceExisting_DeletesReservedJob`). The first-match order of the scan (AC-003.4) is still untested
