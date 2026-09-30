# Findings: Dispatching

- F-001 inconsistency: the XML docs on `PublishResult.JobId` say it returns the custom ID whenever the event implements `ICustomIdEvent`. The code only does that when `CustomId` is non-empty, and falls back to the Hangfire ID otherwise (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs:136-142`). `SetCustomId` also writes a null or empty `CustomId` job parameter (`HangfireDispatcher.cs:149-158`). There's no test for either behavior.
  - Related: REQ-004 (AC-004.3)
  - Resolution: fixed by TASK-003 (AC-004.3)

- F-002 possible bug: enqueueing and saving the custom ID parameter aren't atomic (`HangfireDispatcher.cs:44-45`, and the same in both `Schedule` overloads). If `SetJobParameter` throws, `Publish` throws even though the job already exists. A caller that retries creates a duplicate, and with `SkipIfExists` the duplicate won't be caught because the first job's `CustomId` was never saved. A worker can also pick up the job before the parameter is written. That case is mitigated because the parameter is written again when the job runs (`HangfireJobDispatcher.cs:54-55`).
  - Related: REQ-004, RN-004
  - Resolution: fixed by TASK-005 (REQ-007)

- F-003 possible bug: `Publish`/`Schedule` don't validate `event`, so a null event is enqueued and reaches the handler as `Event == null` (`HangfireDispatcher.cs:38-83`).
  - Related: REQ-001..REQ-003
  - Resolution: fixed by TASK-002 (AC-001.3, AC-002.2, AC-003.2)

- F-004 untested: deduplication is only tested through `Publish`. The same code path in both `Schedule` overloads (`HangfireDispatcher.cs:58, 76`) has no test. `SetCustomId` isn't tested with a null or empty `CustomId` either.
  - Related: RN-003, AC-004.3
  - Resolution: fixed by TASK-007

- F-005 untested: nothing tests that `ICustomNameEvent.CustomName` is written to the job arguments at publish time (`HangfireDispatcher.cs:43-44, 144-147`), or that it's exposed as the `CustomName` flow parameter during execution (`HangfireJobDispatcher.cs:41`). Only the read side in `HangfireJobInfo` is tested.
  - Related: REQ-005
  - Resolution: fixed by TASK-007 (AC-005.1, AC-005.2)

- F-006 bug or rule: handlers are routed by the compile-time `TEvent`, not by the runtime event type (`HangfireJobDispatcher.cs:32`). For example, `dispatcher.Publish((object)evt)` or `Publish<IMyEvent>(evt)` compiles fine but fails at execution time, after retries.
  - Related: RN-001
  - Resolution: accepted as rule (RN-001, DEC-001)

- F-007 untested: nothing tests that `DispatchEventAsync` throws when the handler can't be activated (`HangfireJobDispatcher.cs:36-38`).
  - Related: AC-006.4
  - Resolution: fixed by TASK-007 (AC-006.4)

- F-008 debt: `Publish` and both `Schedule` overloads repeat the same dedup → name → enqueue → set custom ID → result sequence, and differ only in the Hangfire call (`HangfireDispatcher.cs:38-83`). That's why F-002 and F-004 affect all three.
  - Resolution: fixed by TASK-001

- F-009 bug: with deduplication enabled, an `ICustomIdEvent` whose `CustomId` is null or empty reaches `IsRunning(null)`/`IsPending(null)` (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs:121-133`). `HangfireExecutionManager.MatchesId` then compares `GetCustomId(...) == null`, which is true for **every job without a custom ID** (`HangfireExecutionManager.cs:201-207`). With `SkipIfExists` the event is silently dropped whenever any job is running. With `ReplaceExisting` an **unrelated job is cancelled**. The same happens when a caller passes null or empty to `IsRunning`, `IsPending`, `Cancel` or `Retry`.
  - Related: AC-004.3, `execution-manager`
  - Resolution: fixed by TASK-003
