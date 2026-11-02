# Data Model: Handler configuration classes

## IHandlerConfiguration<THandler> (new)

`void Configure(HandlerBuilder<THandler> handler)`; `THandler : class, IHandler`. Created with its parameterless constructor by `ApplyConfigurationsFromAssembly`.

## HandlerBuilder / HandlerBuilder<THandler> (new, replaces ExecutionPlanEntry)

| Item | Methods | Stored as | Rule |
|---|---|---|---|
| Schedule | `Cron(string)`, `Interval(TimeSpan)` | one of cron / interval | one clears the other; interval ≥ 1 min (012) |
| Id | `Id(string)` | string | non-empty |
| Time zone | `TimeZone(string)` | string | null/empty throws; unknown throws at configuration; ignored with warning on intervals |
| Display name | `DisplayName(string)` | string | non-empty (as today) |
| Enabled | `Enabled(bool = true)` | bool? | unset → option/global (AC-002.6) |
| Dependencies | `DependsOn<X>()`, `RemoveDependency<X>()`, `ClearDependencies()` | list | attribute loaded first; additive |
| Run mode | `RunMode(DependentRunMode, TimeSpan?)` | mode + interval | 013 |
| Overlap | `NoOverlap(TimeSpan)`, `AllowOverlap()` | not set / timeout / allowed | R-002 |
| Provider settings | `HasSetting(string, object)` | dictionary | keys prefixed by provider |

## ExecutionPlanStep (changed)

Existing members kept; new: `Cron`, `Interval`, `Id`, `TimeZone`, `ConfiguredEnabled` (bool?), `Settings`. `OverlapTimeout` becomes builder (R-002) > attribute.

## ExecutionPlan (changed)

Records the `MissingPrerequisite` policy used by `Build`, for the re-evaluation of availability at configuration (R-003).

## Resolution per item (providers)

```text
builder (step)  >  per-handler option (SetJobAutoRun<T>, SetJobTimeZone<T>)  >  attribute  >  global default
  └─ overriding an option → trace warning
```
