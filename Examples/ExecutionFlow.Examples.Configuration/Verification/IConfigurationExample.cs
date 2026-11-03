using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Verification;

/// <summary>One way to configure recurring handlers: the configuration a user would write, and what it should produce.</summary>
public interface IConfigurationExample
{
    /// <summary>A short title shown when the example runs.</summary>
    string Title { get; }

    /// <summary>The configuration, as it would appear in an app.</summary>
    void Configure(HangfireOptions options);

    /// <summary>Checks that the configuration resolved as documented.</summary>
    void Verify(Expectations expect, ResolvedConfiguration result);
}
