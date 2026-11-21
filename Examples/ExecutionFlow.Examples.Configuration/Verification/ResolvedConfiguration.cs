using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Verification;

/// <summary>What a configuration resolved to, with readable accessors for the checks.</summary>
public class ResolvedConfiguration
{
    public ResolvedConfiguration(HangfireSetup setup)
    {
        Setup = setup;
    }

    public HangfireSetup Setup { get; }

    public ExecutionPlan? Plan => Setup.Options.Plan;

    /// <summary>The handler's step in the plan.</summary>
    public ExecutionPlanStep Step<THandler>() where THandler : class, IHandler => Plan!.Get<THandler>();

    /// <summary>The handler's registration: its schedule and ID after every layer was applied.</summary>
    public RecurringJobRegistryInfo Registration<THandler>() => Setup.RecurringHandlers[typeof(THandler)];

    /// <summary>The names of the handlers <typeparamref name="THandler"/> waits for, e.g. "CategorySyncHandler, BrandSyncHandler".</summary>
    public string PrerequisitesOf<THandler>() where THandler : class, IHandler =>
        string.Join(", ", Step<THandler>().Prerequisites.Select(t => t.Name));

    /// <summary>How the dependent runs: "triggered", "triggered, at least 00:20:00 apart" or "own schedule".</summary>
    public string RunModeOf<THandler>() where THandler : class, IHandler
    {
        var step = Step<THandler>();
        if (step.RunsOnOwnSchedule)
            return "own schedule";

        return step.MinInterval.HasValue ? $"triggered, at least {step.MinInterval} apart" : "triggered";
    }
}
