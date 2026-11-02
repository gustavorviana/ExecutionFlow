# Quickstart validation: Handler configuration classes

## Prerequisites

```
dotnet restore
dotnet build --no-restore
```

## Scenarios

1. **Configuration class** — `dotnet test --no-build --filter "FullyQualifiedName~HandlerConfigurationTests"`. Expect: every builder value reaches the step; inline `Add<T>()` returns the same builder; a handler configured twice throws (FR-001).
2. **Item-by-item precedence** — same filter. Expect: the builder changes only what it calls; dependencies merge and can be removed; `AllowOverlap()` cancels `[NoOverlap]` (FR-002).
3. **Internal vs user planner** — `--filter "FullyQualifiedName~HandlerConfigurationHangfireTests"`. Expect: without a user plan, `GetPlanOf<T>()` (and the obsolete `SetJobAutoRun`/`SetJobTimeZone`) apply; a handler in the user plan drops `GetPlanOf` as a whole with one warning, while its attributes still apply (FR-006).
4. **Assembly scan** — `ApplyConfigurationsFromAssembly` applies every configuration class, skips abstract ones, throws without a parameterless constructor (FR-003).
5. **Hangfire extensions** — `handler.Queue("reports")` and `.AutomaticRetry(...)` win over the class's attributes (FR-004).
6. **Shortcut** — `UsePlanFromAssembly(assembly, predicate, policy)` (FR-005).
7. **Regression** — full `dotnet test`: only the tests of the deprecated AC-002.6/AC-002.7 change (T022); the build shows obsolete warnings only where `SetJobAutoRun`/`SetJobTimeZone` are still called.
