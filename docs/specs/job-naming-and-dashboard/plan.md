# Plan: Job naming and dashboard (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–5)
Status: approved

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `ICustomNameEvent` moves here (namespace `ExecutionFlow.Abstractions`) | **Yes (source)**: `using ExecutionFlow.Abstractions;` is needed |
| `ExecutionFlow.Hangfire` | `HangfireJobInfo.Create` returns `null` for non-ExecutionFlow jobs. `DefaultHangfireJobName` no longer uses `IJobIdGenerator`, and non-ExecutionFlow jobs get Hangfire's default name. `[JobDisplayName]` on handlers is read. The dashboard falls back to default names | Behavior |

## Design
- **Recognizing ExecutionFlow jobs:** `HangfireJobInfo.Create(job)` returns `HangfireEventJobInfo` only for `job.IsEvent()`, `HangfireRecurringJobInfo` only for `job.IsRecurring()`, and `null` otherwise. Callers already handle `null`.
- **Name chain for ExecutionFlow jobs** (`HangfireJobInfo.GetExpectedName`). `[JobDisplayName]` is read from the handler's `HandleAsync`, because the attribute only applies to methods (F-006):
  1. `CustomName` (event jobs)
  2. `[DisplayName]` on the registered handler
  3. `[JobDisplayName]` on the registered handler, with `{0}` replaced by the event
  4. the registry display name (the handler class name)
  5. the event type's `[DisplayName]` or name (event jobs), or the handler type's (recurring jobs)
- **`[JobDisplayName]` formatting:** `string.Format(CultureInfo.CurrentCulture, text, event)` for event jobs. For recurring jobs, or if formatting throws (for example because of an unknown placeholder), the text is used as is.
- **Default name for other jobs:** `[JobDisplayName]` on the job method, formatted with the job's arguments, with the text as is on a format error. Otherwise `job.ToString()`, which is Hangfire's `Type.Method`.
- **Dashboard:** with the `IServiceProvider` overload, a missing `IHangfireJobName` → the default name, with no exception and no warning.
- **`DefaultHangfireJobName` constructor:** keeps the `(IJobIdGenerator, IExecutionFlowRegistry)` signature, because it's public and DI constructs it. The generator is ignored and may be null.

## Decisions
### DEC-001: `{0}` in a handler's `[JobDisplayName]` is the event
- Context: Hangfire fills placeholders with the job method's arguments. For ExecutionFlow those are the dispatcher's internal arguments.
- Decision: `{0}` = the event. Other placeholders, or recurring jobs, → the text as is. This was the proposal left open in the spec, and it was applied when the maintainer asked to implement without choosing.

## Versioning impact
1.2.0 (source break on `ICustomNameEvent`'s namespace).

## Revision (2026-09-30): one owner for the naming rules
Requested by the maintainer after the first implementation, to remove duplicated naming logic between `HangfireJobInfo` and `DefaultHangfireJobName`.

- **`JobDisplayNameResolver`** (new, concrete, public, `ExecutionFlow.Hangfire`) owns the rules:
  - `protected string GetConfiguredName(Job)` is non-virtual, with the fixed order `CustomName` > handler `[DisplayName]` > `[DisplayName]` of the carried type when the handler isn't registered. It returns `null` when nothing is configured, and always `null` for native jobs.
  - `protected string GetFallbackName(Job)` = `GenerateId(registered handler type ?? carried type ?? job.Type)`.
  - `public virtual string GetName(Job)` = configured ?? fallback.
- **`DefaultHangfireJobName : JobDisplayNameResolver, IHangfireJobName`** overrides `GetName` = configured ?? `[JobDisplayName]` ?? fallback. It holds the only `[JobDisplayName]` reader and formatter, shared by ExecutionFlow jobs (`HandleAsync`, `{0}` = the event), native jobs (method, job arguments) and the dashboard's no-`IHangfireJobName` fallback (`GetHangfireDefaultName`).
- **`HangfireJobInfo`** only exposes job facts: `CarriedType`, `HangfireEventJobInfo.Event`/`CustomJobName`, `GetHandler`. Removed: `GetExpectedName` (both overloads and overrides), `GetTypeDisplayName`, and the `GetDefinedName`/`GetNamingType` added earlier in this cycle.
- **Rule change:** `[JobDisplayName]` moves after every configured name (it was between the handler's `[DisplayName]` and the event's).

## Revision 2 (2026-09-30): each step is its own protected method
Requested by the maintainer so Hangfire's attribute can come **before** the handler class's `[DisplayName]`.
- `[DisplayName]` belongs on classes (handler, event). Only `[JobDisplayName]` belongs on a method (`HandleAsync`).
- `JobDisplayNameResolver` exposes one protected method per step: `GetConfiguredName` (custom name only), `GetHandlerDisplayName`, `GetCarriedTypeDisplayName` (only when the handler isn't registered), `GetFallbackName`. `GetName` chains them.
- `DefaultHangfireJobName.GetName` = custom name > `[JobDisplayName]` > handler class `[DisplayName]` > carried class `[DisplayName]` > `GenerateId`.
