# Plan: Dispatching v1.2.0

Spec: [spec.md](spec.md)
Status: approved

## Summary of the change
Fixes the defects found while mapping dispatching ([findings.md](findings.md)), and documents the handler routing rule. Implements AC-001.3, AC-002.2, AC-003.2, AC-004.3, REQ-007 and REQ-008, and adds tests for criteria that had none.

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | XML docs on `IEventDispatcher` and `PublishResult` | No |
| `ExecutionFlow.Hangfire` | `HangfireDispatcher` pipeline, `HangfireExecutionManager` null guards, `HandlerJobFilterProvider`, new `HangfireOptions.RetryUnregisteredEventJobs` | No API break. Behavior changes for null events, empty `CustomId`, and retries of jobs without a handler |

## Design
- `HangfireDispatcher.Publish` and both `Schedule` overloads delegate to one private `Dispatch<TEvent>(TEvent, IState)`. The steps are: null guard, dedup (only with a non-empty custom ID), then job creation.
- Job creation: when the client implements `IBackgroundJobClientV2`, the job is built with `Job.FromExpression` and created with `Create(job, state, parameters)`, which stores the `CustomId` parameter in the same storage transaction. Other clients use `Create(job, state)` and then `SetJobParameter`.
- `JobParameters` (internal) is the single place that reads and writes the `CustomId` parameter, so the encoding stays consistent and legacy values stay readable.
- `HandlerJobFilterProvider` adds `AutomaticRetryAttribute { Attempts = 0 }` for ExecutionFlow event jobs whose handler isn't registered on this host, unless `RetryUnregisteredEventJobs` is set.

## Decisions
### DEC-001: Keep routing by compile-time type
- Context: F-006. `Publish((object)evt)` compiles but fails at execution.
- Options considered: (1) route by runtime type with fallback to base classes and interfaces; (2) keep it and document it; (3) route by runtime type only.
- Decision: (2). The maintainer chose to keep the current contract and not add routing complexity.
- Consequences: generic code that publishes `object` or interface-typed events must call `Publish` with the concrete type. The rule is documented in RN-001 and the README, and covered by a test.
- Related requirements: RN-001

### DEC-002: Fail jobs with no registered handler without retrying, by default
- Context: RN-005. Retrying doesn't fix a missing registration, so 10 retries only delay the failure.
- Options considered: fail always; keep retries; an option with fail as the default.
- Decision: the new option `HangfireOptions.RetryUnregisteredEventJobs`, default `false`. Deployments where consumers with different handlers share one queue can set it to `true`.
- Consequences: a behavior change, listed in the release notes.
- Related requirements: REQ-008

### DEC-003: Null event is rejected
- Decision: throw `ArgumentNullException` before any storage access.
- Related requirements: AC-001.3, AC-002.2, AC-003.2

### DEC-004: Atomic job creation through `IBackgroundJobClientV2`
- Context: F-002.
- Decision: use `IBackgroundJobClientV2.Create(job, state, parameters)` (Hangfire 1.8+) when available, and fall back to two steps otherwise.
- Spike result (TASK-004): with Hangfire 1.8.25, V2 stores parameter values JSON-encoded (`CustomId = "abc"` is stored as `"\"abc\""`), while 1.1.0 stored the raw string. So every write now goes through `JobParameters.WriteCustomId`, which uses JSON (Hangfire's own convention for job parameters), and every read goes through `JobParameters.ReadCustomId`, which accepts both formats.
- Consequences: jobs created by 1.1.0 stay findable by custom ID. The reverse isn't true: a host still on 1.1.0 reads new values with the quotes, so during a mixed-version rollout its `IsRunning`/`Cancel`/dedup by custom ID miss jobs created by 1.2.0. Upgrade all hosts that share a storage together.
- Related requirements: REQ-007

## Versioning impact
Minor: 1.2.0 (lockstep).

## Risks
| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Parameter encoding differs between V2 and `SetJobParameter`, which would break lookups of jobs already in storage | `IsRunning`/`Cancel` by custom ID miss old jobs | A spike (TASK-004), and `JobParameters.ReadCustomId` accepting both formats |
| Users relying on retries for jobs with no handler | Jobs fail sooner | `RetryUnregisteredEventJobs = true`, and release notes |
| Custom `IBackgroundJobClient` without V2 | Not atomic | Fallback path, documented in AC-007.2 |
