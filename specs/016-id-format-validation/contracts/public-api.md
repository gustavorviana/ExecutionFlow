# Public API contract: ID format validation

No public types or members are added or removed. The change is in the documented behavior of existing members.

## Behavior changes (breaking, confirmed, ships in 2.0.0)

| Member | Before | After |
|--------|--------|-------|
| `IEventDispatcher.Publish<TEvent>` / `Schedule<TEvent>` (both overloads) | any non-empty custom ID accepted | custom ID outside the ID format → `ArgumentException`, no job created |
| `IExecutionManager.IsRunning(string)` / `IsPending(string)` / `Cancel(string)` / `Retry(string)` | `null`/empty → `false`; any other string searched | `null` → `ArgumentNullException`; empty, only spaces or outside the format → `ArgumentException` |
| `IRecurringTrigger.Trigger(string)` | `null`/empty → `ArgumentNullException`; any other string triggered | `null` → `ArgumentNullException`; empty, only spaces or outside the format → `ArgumentException` (empty was `ArgumentNullException`) |
| `[Recurring(Id = ...)]`, `[RecurringInterval(Id = ...)]` | any string | outside the format → `ArgumentException` when the handler is registered |
| `HandlerBuilder.Id(string)` | `null`/whitespace → `ArgumentException` | `null` → `ArgumentNullException` (subclass of `ArgumentException`); outside the format → `ArgumentException` |
| `IJobIdGenerator.GenerateId` result | any string | outside the format → `ArgumentException` naming the handler, when recurring jobs are resolved |

Unchanged: the `Type` overloads (`IsRunning(Type)`, `Cancel(Type)`, `Retry(Type)`, `Trigger(Type)`) except that the ID they resolve follows the generated-ID rule; `null`/empty custom ID = no custom ID.

## XML docs to update (FR-005)

`ICustomIdEvent.CustomId`, `RecurringAttribute.Id`, `RecurringIntervalAttribute.Id`, `HandlerBuilder.Id`, `IJobIdGenerator.GenerateId`, `IExecutionManager` (the four string methods), `IRecurringTrigger.Trigger(string)`, `IEventDispatcher.Publish`/`Schedule`: add `<exception cref="ArgumentException">` and a sentence with the format.
