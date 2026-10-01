# Plan: Local hosting (v1.3.0)

Spec: [spec.md](spec.md)
Status: draft

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow.Hosting` (new) | `HostingOptions`, `ServiceCollectionExtensions.AddExecutionFlowHosting`, `IExecutionPlanRunner`, `RunOnceResult`, `HandlerRunOutcome`, `HandlerRunStatus` | New package |

## Design
- **Target frameworks:** `netstandard2.0;net8.0`.
  - On `net8.0`, all time comes from `TimeProvider` (`GetUtcNow`, `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, `Task.WaitAsync(TimeSpan, TimeProvider, CancellationToken)`).
  - On `netstandard2.0`, an internal `SystemClock` polyfill wraps `DateTimeOffset.UtcNow` and `Task.Delay`, and implements the timeout wait with `Task.WhenAny`.
  - Both targets go through one internal `IClock` seam, and `#if NET8_0_OR_GREATER` picks the implementation.
- **Dependencies:** `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` (lower bound 6.0.0), and `Cronos` for cron.
- **`DependencyGate`** (singleton):
  - one `Gate` per handler `Type`, holding a `long` generation and a `TaskCompletionSource` that is swapped on every `Signal`;
  - the generation and the task are read together under the gate's lock;
  - the old TCS is completed outside the lock, and is created with `RunContinuationsAsynchronously`.
- **`RecurringHandlerHost<THandler>`** (`BackgroundService`, one per enabled handler, registered as `IHostedService` through a factory, with the handler type closed by `MakeGenericType`), in a loop:
  1. compute the next occurrence;
  2. delay until then;
  3. wait on the gate for every prerequisite, and remember the consumed generations;
  4. take the limiter;
  5. run inside a new scope;
  6. release the limiter;
  7. on success, `Signal`.
- **`HandlerExecutor`**: shared by the scheduler and the runner. It opens the scope, resolves the handler, builds the `FlowContext` with `FlowContextBuilder` + `ExecutionLoggerFactory`, calls `HandleAsync` and disposes the context.
- **`ExecutionPlanRunner`**: runs `ExecutionOrder` sequentially, keeping the set of handlers that completed.
- **`RunOnceHostedService`**: runs from `StartAsync` on a background task, so the host startup isn't blocked, then sets `Environment.ExitCode` and calls `StopApplication`.
- **Schedule:**
  - cron is parsed by `Cronos`, in 5-field format, or 6-field when the expression has 6 fields;
  - the next occurrence comes from `GetNextOccurrence(now, timeZone)`;
  - an interval schedule's next occurrence is `now + interval`.

## Decisions
### DEC-001: A separate provider package
Promoted to [ADR-0008](../adr/ADR-0008-local-hosting-provider.md).

### DEC-002: Wait for prerequisites instead of skipping
- Context: Hangfire can't hold a worker while it waits, so there a dependent run is skipped. The local scheduler can wait for free.
- Decision: the local scheduler waits, with a periodic report. The semantics (a new generation of every prerequisite before each run) match Hangfire.
- Consequences: locally, a dependent runs as soon as its prerequisite completes, instead of at its next occurrence.

### DEC-003: `MakeGenericType` for hosted services
- Context: handlers come from `Scan`, which already uses reflection.
- Decision: register `RecurringHandlerHost<T>` through `MakeGenericType`.
- Consequences: the package isn't trimming-safe, the same as the core `Scan`.
