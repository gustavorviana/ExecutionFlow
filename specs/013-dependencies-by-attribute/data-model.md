# Data Model: Dependencies by attribute

## DependentRunMode (new enum, core)

`MinInterval` | `OwnSchedule` — the two non-default run modes of 010 FR-007. Absence of a mode means the default (triggered by prerequisites).

## DependentRunAttribute (new, core)

| Field | Type | Rule |
|---|---|---|
| `Mode` | `DependentRunMode` | required (constructor) |
| `Interval` | string, optional | `[d.]hh:mm:ss`, positive; required for `MinInterval`, forbidden for `OwnSchedule`; invalid → `Build` throws naming handler and value |

- At most one per class (`AllowMultiple = false`), on the dependent handler.
- On a handler without prerequisites → invalid (010 AC-007.4).

## ExecutionPlanEntry (changed)

- New `RunMode(DependentRunMode, TimeSpan? interval = null)`; same mode/interval rules as the attribute. Replaces `MinInterval(TimeSpan)` and `RunOnOwnSchedule()`.
- Keeps the attribute's mode (read by `ExecutionPlanner.Add`) apart from the fluent one. Effective mode at `Build`: the fluent mode if `RunMode` was called, else the attribute's (research R-002).

## ExecutionFlowOptions (changed)

| Member | Kind | Purpose |
|---|---|---|
| `AutomaticPlanMissingPrerequisite` | public property, default `Throw` | policy for the automatic plan only |
| `Plan` | existing getter | holds the explicit or the automatic plan |
| `IsRecurringHandlerEnabled(Type)` | protected internal virtual, default `true` | provider's on/off for a step |
| `OnAutomaticPlanFormed(ExecutionPlan)` | protected virtual | provider applies skipped steps |
| registration order | internal list | keeps `Scan`/`Add` order for the automatic plan |

## Lifecycle

```text
configure(options)  →  [no Plan and some [DependsOn]/[DependentRun]?]
                    →  ExecutionPlanner(all registered recurring handlers in registration order,
                                        Enabled = provider's on/off)
                    →  Build(AutomaticPlanMissingPrerequisite)
                    →  Plan = automatic; OnAutomaticPlanFormed
                    →  Lock()  →  OnConfigured (provider validation, as today)
```
