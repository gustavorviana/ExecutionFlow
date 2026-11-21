using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>
/// setup-and-configuration to-be item 1: one active full Hangfire setup per process (the last build wins), and a setup
/// never uses another setup's activator. These touch Hangfire's statics, so they run alone and restore them.
/// </summary>
[Collection(GlobalHangfireStateCollection.Name)]
public class HangfireSetupGlobalStateTests : IDisposable
{
    private readonly List<object> _filtersBefore = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
    private readonly List<IJobFilterProvider> _providersBefore = JobFilterProviders.Providers.ToList();
    private readonly JobActivator _activatorBefore = JobActivator.Current;

    public void Dispose()
    {
        foreach (var instance in GlobalJobFilters.Filters.Select(f => f.Instance).Except(_filtersBefore).ToList())
            GlobalJobFilters.Filters.Remove(instance);
        foreach (var provider in JobFilterProviders.Providers.Except(_providersBefore).ToList())
            JobFilterProviders.Providers.Remove(provider);
        JobActivator.Current = _activatorBefore;
    }

    private static JobStorage CreateStorage()
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());
        return storage;
    }

    private static HangfireSetup CreateConfigured()
    {
        var setup = new HangfireSetup();
        setup.Configure(o => { });
        return setup;
    }

    [Fact]
    public void SecondFullBuild_ReplacesTheFirstSetupsGlobalFilters()
    {
        var first = CreateConfigured();
        var second = CreateConfigured();

        first.Build(Substitute.For<IBackgroundJobClient>(), CreateStorage());
        second.Build(Substitute.For<IBackgroundJobClient>(), CreateStorage());

        var added = GlobalJobFilters.Filters.Select(f => f.Instance).Except(_filtersBefore).ToList();
        Assert.Single(added.OfType<HangfireStateFilter>());
        Assert.Single(added.OfType<DeduplicationCleanupFilter>());
        Assert.Single(JobFilterProviders.Providers.Except(_providersBefore));
    }

    [Fact]
    public void Build_WithoutProvider_NeverUsesAnotherSetupsActivator()
    {
        var other = CreateConfigured();
        other.ConfigureActivator();                       // JobActivator.Current now belongs to "other"
        var otherActivator = (IServiceProvider)JobActivator.Current;

        var setup = CreateConfigured();
        setup.Build(Substitute.For<IBackgroundJobClient>(), CreateStorage());

        // Before the fix, this build registered its dispatcher into the other setup's activator. The built-in
        // activator throws for interfaces nobody registered, so a throw proves nothing was registered there.
        Assert.Throws<InvalidOperationException>(() => otherActivator.GetService(typeof(IEventDispatcher)));
    }

    [Fact]
    public void ConfigureActivator_InstallsTheActivatorUsedByBuild()
    {
        var setup = CreateConfigured();
        var dispatcher = setup.Build(Substitute.For<IBackgroundJobClient>(), CreateStorage());

        setup.ConfigureActivator();

        Assert.Same(dispatcher, ((IServiceProvider)JobActivator.Current).GetService(typeof(IEventDispatcher)));
        Assert.Same(setup.ExecutionManager, ((IServiceProvider)JobActivator.Current).GetService(typeof(IExecutionManager)));
    }
}
