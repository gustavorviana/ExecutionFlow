# Research: Dispatching

Decisions for [spec.md](spec.md). `R-001`..`R-004` were `DEC-001`..`DEC-004` of the old plan. The brownfield findings follow.

## R-001: Keep routing by compile-time type (was DEC-001)

- **Context**: F-006. `Publish((object)evt)` compiles but fails at execution.
- **Decision**: keep the current contract and document it. The maintainer chose not to add routing complexity.
- **Rationale**: generic code that publishes `object` or interface-typed events must call `Publish` with the concrete type. The rule is documented in RN-001 and the README, and covered by a test.
- **Alternatives considered**: (1) route by runtime type with fallback to base classes and interfaces; (3) route by runtime type only. Both rejected.
- **Related requirements**: RN-001

## R-002: Fail jobs with no registered handler without retrying, by default (was DEC-002)

- **Context**: RN-005 (deprecated). Retrying doesn't fix a missing registration, so 10 retries only delay the failure.
- **Decision**: the new option `HangfireOptions.RetryUnregisteredEventJobs`, default `false`. Deployments where consumers with different handlers share one queue can set it to `true`.
- **Rationale**: a behavior change, listed in the release notes.
- **Alternatives considered**: fail always; keep retries.
- **Related requirements**: FR-008

## R-003: Null event is rejected (was DEC-003)

- **Decision**: throw `ArgumentNullException` before any storage access.
- **Rationale**: a null event would otherwise be enqueued and reach the handler as `Event == null` (F-003).
- **Alternatives considered**: none recorded.
- **Related requirements**: AC-001.3, AC-002.2, AC-003.2

## R-004: Atomic job creation through `IBackgroundJobClientV2` (was DEC-004)

- **Context**: F-002.
- **Decision**: use `IBackgroundJobClientV2.Create(job, state, parameters)` (Hangfire 1.8+) when available, and fall back to two steps otherwise.
- **Spike result (T004)**: with Hangfire 1.8.25, V2 stores parameter values JSON-encoded (`CustomId = "abc"` is stored as `"\"abc\""`), while 1.1.0 stored the raw string. So every write now goes through `JobParameters.WriteCustomId`, which uses JSON (Hangfire's own convention for job parameters), and every read goes through `JobParameters.ReadCustomId`, which accepts both formats.
- **Rationale / consequences**: jobs created by 1.1.0 stay findable by custom ID. The reverse isn't true: a host still on 1.1.0 reads new values with the quotes, so during a mixed-version rollout its `IsRunning`/`Cancel`/dedup by custom ID miss jobs created by 1.2.0. Upgrade all hosts that share a storage together.
- **Alternatives considered**: keep the two-step create + `SetJobParameter` everywhere (not atomic).
- **Related requirements**: FR-007

## Findings (brownfield, was findings.md)

- **F-001** inconsistency: the XML docs on `PublishResult.JobId` say it returns the custom ID whenever the event implements `ICustomIdEvent`. The code only does that when `CustomId` is non-empty, and falls back to the Hangfire ID otherwise (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs:136-142`). `SetCustomId` also writes a null or empty `CustomId` job parameter (`HangfireDispatcher.cs:149-158`). There's no test for either behavior.
  - Related: FR-004 (AC-004.3)
  - Resolution: fixed by T003 (AC-004.3)

- **F-002** possible bug: enqueueing and saving the custom ID parameter aren't atomic (`HangfireDispatcher.cs:44-45`, and the same in both `Schedule` overloads). If `SetJobParameter` throws, `Publish` throws even though the job already exists. A caller that retries creates a duplicate, and with `SkipIfExists` the duplicate won't be caught because the first job's `CustomId` was never saved. A worker can also pick up the job before the parameter is written. That case is mitigated because the parameter is written again when the job runs (`HangfireJobDispatcher.cs:54-55`).
  - Related: FR-004, RN-004
  - Resolution: fixed by T005 (FR-007)

- **F-003** possible bug: `Publish`/`Schedule` don't validate `event`, so a null event is enqueued and reaches the handler as `Event == null` (`HangfireDispatcher.cs:38-83`).
  - Related: FR-001..FR-003
  - Resolution: fixed by T002 (AC-001.3, AC-002.2, AC-003.2)

- **F-004** untested: deduplication is only tested through `Publish`. The same code path in both `Schedule` overloads (`HangfireDispatcher.cs:58, 76`) has no test. `SetCustomId` isn't tested with a null or empty `CustomId` either.
  - Related: RN-003, AC-004.3
  - Resolution: fixed by T007

- **F-005** untested: nothing tests that `ICustomNameEvent.CustomName` is written to the job arguments at publish time (`HangfireDispatcher.cs:43-44, 144-147`), or that it's exposed as the `CustomName` flow parameter during execution (`HangfireJobDispatcher.cs:41`). Only the read side in `HangfireJobInfo` is tested.
  - Related: FR-005
  - Resolution: fixed by T007 (AC-005.1, AC-005.2)

- **F-006** bug or rule: handlers are routed by the compile-time `TEvent`, not by the runtime event type (`HangfireJobDispatcher.cs:32`). For example, `dispatcher.Publish((object)evt)` or `Publish<IMyEvent>(evt)` compiles fine but fails at execution time, after retries.
  - Related: RN-001
  - Resolution: accepted as rule (RN-001, R-001)

- **F-007** untested: nothing tests that `DispatchEventAsync` throws when the handler can't be activated (`HangfireJobDispatcher.cs:36-38`).
  - Related: AC-006.4
  - Resolution: fixed by T007 (AC-006.4)

- **F-008** debt: `Publish` and both `Schedule` overloads repeat the same dedup → name → enqueue → set custom ID → result sequence, and differ only in the Hangfire call (`HangfireDispatcher.cs:38-83`). That's why F-002 and F-004 affect all three.
  - Resolution: fixed by T001

- **F-009** bug: with deduplication enabled, an `ICustomIdEvent` whose `CustomId` is null or empty reaches `IsRunning(null)`/`IsPending(null)` (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs:121-133`). `HangfireExecutionManager.MatchesId` then compares `GetCustomId(...) == null`, which is true for **every job without a custom ID** (`HangfireExecutionManager.cs:201-207`). With `SkipIfExists` the event is silently dropped whenever any job is running. With `ReplaceExisting` an **unrelated job is cancelled**. The same happens when a caller passes null or empty to `IsRunning`, `IsPending`, `Cancel` or `Retry`.
  - Related: AC-004.3, [007-execution-manager](../007-execution-manager/spec.md)
  - Resolution: fixed by T003
