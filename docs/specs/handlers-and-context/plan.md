# Plan: Handlers and context (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–4)
Status: approved

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `FlowContext.JobId`, `FlowContext.AttemptNumber`, `FlowContextBuilder.SetJob(string, int)` | No (additions). Behavior: case-variant writes to read-only keys now throw |
| `ExecutionFlow.Hangfire` | `FlowContextExtensions.GetPerformContext()`, and `HangfireJobDispatcher` sets the job facts | No |

## Design
- `FlowParameters._readOnlyKeys` uses `StringComparer.OrdinalIgnoreCase`, like `_items`.
- `FlowContextBuilder.SetJob(jobId, attemptNumber)` stores the processor's job facts. `Build` passes them to the context through internal setters. Defaults: `JobId = null`, `AttemptNumber = 1`.
- `HangfireJobDispatcher.CreateContextBuilder` calls `SetJob(performContext.BackgroundJob.Id, RetryCount + 1)`. `RetryCount` is read like the state filter reads it (raw parameter, `int.TryParse`, 0 on failure). A null `PerformContext` leaves the defaults.
- Keys: no infrastructure key is public (maintainer's decision). They stay internal in `ContextConsts`: `GetPerformContext()` covers Hangfire access, and the custom name is on the event (`ICustomNameEvent`).
- `FlowContextExtensions.GetPerformContext(this FlowContext)` returns the `PerformContext` or `null`.

## Decisions
### DEC-001: `AttemptNumber` starts at 1
- The first run is attempt 1, and the first retry is attempt 2. This matches `ExecutionRetryingEvent.AttemptNumber` semantics and common log formats.

## Versioning impact
1.2.0.
