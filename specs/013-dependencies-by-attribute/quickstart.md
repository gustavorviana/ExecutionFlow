# Quickstart validation: Dependencies by attribute

## Prerequisites

```
dotnet restore
dotnet build --no-restore
```

## Scenarios

1. **Automatic plan from attributes** — `dotnet test --no-build --filter "FullyQualifiedName~AutomaticPlan"`. Expect: `B` with `[DependsOn(typeof(A))]` registered by `Scan`, without `UsePlan`, is a dependent of `A` and is registered with `Cron.Never()` in Hangfire (FR-001).
2. **Run mode by attribute** — `--filter "FullyQualifiedName~DependentRunAttributeTests"`. Expect: `MinInterval`/`RunOnOwnSchedule` read from `[DependentRun]`; invalid combinations throw (FR-002).
3. **Validation** — cycles and missing or turned-off prerequisites throw by default; `AutomaticPlanMissingPrerequisite = SkipDependents` turns dependents off in cascade (FR-003).
4. **Explicit plan unchanged** — `--filter "FullyQualifiedName~ExecutionPlanHangfireTests|FullyQualifiedName~PrerequisiteGateTests|FullyQualifiedName~ExecutionPlannerTests"`. Expect: all pass; the entry's mode wins over `[DependentRun]` (FR-004, SC-003).
5. **Manual check in `Examples/`** — remove `options.UsePlan(...)` from a copy of the configurator and put `[DependsOn]` on the plan handlers; the dashboard shows the dependents as `Never` and they run after their prerequisites.
