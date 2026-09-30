# Plan: Recurring jobs (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–7)
Status: approved

## Summary of the change
Fixes the recurring-job defects found in the as-is mapping: F-001, F-002, F-003, F-004, F-005, F-007 and F-008. It adds a stable ID and time zones, and moves the on/off switch to the schedule itself.

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `RecurringAttribute.Id` and `RecurringAttribute.TimeZone` (optional named properties). `RecurringJobRegistryInfo.Id` and `RecurringJobRegistryInfo.TimeZone` | No |
| `ExecutionFlow.Hangfire` | `HangfireOptions.RecurringTimeZone`, `SetJobTimeZone<T>` / `SetJobTimeZone(Type, string)`. `HangfireAutoRunFilter` is `[Obsolete]` and no longer registered | **Behavior**: a recurring handler without `[Recurring]` now fails at `Configure`; on/off is per storage |

## Design
- **Validation in `HangfireSetup.OnConfigured`** (which runs during `Configure`, next to the existing `SetJobAutoRun` check):
  - Every recurring handler must have a cron. Otherwise throw `InvalidOperationException` naming the handler.
  - Every time zone (global, per handler in options, on the attribute) must resolve with `TimeZoneInfo.FindSystemTimeZoneById`.
  - Recurring job IDs must be unique.
- **ID resolution:** `RecurringJobIds.Resolve(registration, generator)` = `registration.Id ?? generator.GenerateId(handlerType)`. It's used by `RegisterRecurring` and by `HangfireDispatcher.Trigger(Type)`.
- **Time zone resolution:** `SetJobTimeZone<T>` > `[Recurring(TimeZone)]` > `RecurringTimeZone` > UTC. It's passed through `RecurringJobOptions.TimeZone`.
- **On/off:** a disabled handler (per-handler setting, else global) is registered with `Cron.Never()`. `HangfireAutoRunFilter` is no longer registered, because with no automatic fires for disabled handlers there's nothing to cancel. That removes the fragile detection of manual triggers by text (F-004).
- **Orphans:** remove only recurring jobs whose `Job.Method` is `HangfireJobDispatcher.DispatchRecurringAsync` and whose ID isn't registered. Jobs that fail to load (`Job == null`) are left alone.

## Decisions
### DEC-001: Remove the auto-run filter instead of fixing its trigger detection
- Context: F-004 (the detection is fragile and may already be broken for the dashboard). Item 6 makes disabled handlers never fire automatically.
- Decision: stop registering the filter, and mark the public type `[Obsolete]`.
- Consequences: to-be item 5 (a stable marker for manual triggers) is no longer needed. Manual triggers of disabled handlers always run.

### DEC-002: Validate in `HangfireSetup.OnConfigured`, not in `ExecutionFlowOptions.Add`
- Context: the cron is only meaningful for scheduling, which the Hangfire package does. The core only collects metadata.
- Decision: validate when `Configure` completes in the Hangfire setup. Configuration errors still surface at startup, before `Build()`.

## Versioning impact
1.2.0.

## Risks
| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Apps with a recurring handler that has no `[Recurring]` | `Configure` now throws | That already failed at `Build()`, so the error just moves earlier and gets clearer |
| Consumers relying on per-consumer on/off | Behavior changes | It was nondeterministic (F-008). Release notes point to queues |
| Time zone IDs differ between Windows (.NET Framework) and IANA | Configuration error on some hosts | A clear message, documented in the README |
