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
| Enabled | `Enabled(bool = true)` | bool? | unset → global default (user plan); `GetPlanOf` value then global (no user plan), FR-006 |
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
handler in the user plan:      user builder (step)  >  attribute  >  global default        (GetPlanOf dropped as a whole, warning)
handler outside the user plan: GetPlanOf builder     >  attribute  >  global default        (automatic plan when it has dependencies)
obsolete SetJobAutoRun<T>/SetJobTimeZone<T>  →  GetPlanOf<T>().Enabled(...)/.TimeZone(...)
```

## Internal planner (FR-006)

| Part | Kind | Purpose |
|---|---|---|
| internal `ExecutionPlanner` | private field of `ExecutionFlowOptions` | holds one `HandlerBuilder<T>` per `GetPlanOf` handler, never built directly |
| `GetPlanOf<T>()` / `GetPlanOf(Type)` | public methods | return (creating on first call) the handler's builder; the class's `[DependsOn]`/`[DependentRun]` are preloaded |
| `IsRecurringHandlerEnabledByDefault()`, `DefaultRecurringTimeZone` | protected virtuals | the provider's global defaults (R-009) |
| `IsEnabled(type)`, `ResolveTimeZoneId(type)` | internal | per-handler resolution used by providers |
