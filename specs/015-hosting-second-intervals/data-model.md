# Data Model: Intervals of 1 second in the local host

No new persisted entity. Changes to existing values and settings:

## Interval schedule (core, existing)

| Field | Rule (before → after) |
|---|---|
| `RecurringJobRegistryInfo.Interval` (`TimeSpan?`) | ≥ 1 min → **≥ 1 s**; exact value kept, fractions included (AC-003.3) |
| `[RecurringInterval]` string | `[d.]hh:mm:ss`, unchanged; minimum ≥ 1 s |

## Effective interval (Hangfire, computed, not stored)

Derived from the declared interval and `SchedulePollingInterval` (research R-004):

| Declared (after truncating to seconds) | Effective | Registered as | Warning |
|---|---|---|---|
| Whole minutes, divides hour/day | unchanged | exact 5-field cron (012) | only if < polling minimum |
| Whole minutes, other | unchanged | per-minute ticker `* * * * *` (012) | only if < polling minimum |
| Seconds part, < polling minimum | polling minimum | from the row below | yes, "raised" |
| Seconds part, divides 60 s | unchanged | `*/n * * * * *` | no |
| Seconds part, other | unchanged | second ticker `* * * * * *` | no |

## Ticker last run (Hangfire storage, existing)

- Hash `executionflow:interval:lastrun`, field = recurring job ID, value = UTC ticks of the truncated start of the last run.
- Truncation: minute (minute path, unchanged) or **second** (second path). Same format, so a switch of path reads a compatible value (AC-005.10).

## Settings (new)

| Setting | Type | Default | Validation |
|---|---|---|---|
| `HangfireOptions.SchedulePollingInterval` | `TimeSpan` | 15 s | ≥ 1 s, else `ArgumentOutOfRangeException`; locked after `Configure` |
| `HostingOptions.LogSkippedOccurrences` | `bool` | `true` | locked after `Configure` |
| Builder setting `Hosting:LogSkippedOccurrences` | `bool` | (unset → global) | set by `handler.LogSkippedOccurrences(bool = true)` |
