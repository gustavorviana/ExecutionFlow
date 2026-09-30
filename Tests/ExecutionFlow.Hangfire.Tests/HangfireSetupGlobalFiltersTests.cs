using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

/// <summary>
/// Tests that touch Hangfire's static <see cref="GlobalJobFilters"/>. They run alone (no parallelization),
/// so the filters added by <see cref="HangfireSetup.Build"/> can be measured exactly and removed afterwards.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class GlobalHangfireStateCollection
{
    public const string Name = "Global Hangfire state";
}

[Collection(GlobalHangfireStateCollection.Name)]
public class HangfireSetupGlobalFiltersTests
{
    [Fact]
    public void Build_RegistersDeduplicationCleanupFilter_Once()
    {
        var before = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        try
        {
            setup.Build(Substitute.For<IBackgroundJobClient>(), storage);

            var added = GlobalJobFilters.Filters.Select(f => f.Instance).Except(before).ToList();
            Assert.Single(added.OfType<DeduplicationCleanupFilter>());
        }
        finally
        {
            RemoveAddedFilters(before);
        }
    }

    [Fact]
    public void Build_RegistersStateFilter_AfterHangfireRetryFilter()
    {
        var before = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        try
        {
            setup.Build(Substitute.For<IBackgroundJobClient>(), storage, new ExecutionFlow.Hangfire.Infrastructure.FlowEngineJobActivator(setup));

            var stateFilter = GlobalJobFilters.Filters.Single(f => f.Instance is HangfireStateFilter && !before.Contains(f.Instance));
            var retryFilter = GlobalJobFilters.Filters.Single(f => f.Instance is AutomaticRetryAttribute);
            Assert.Equal(HangfireStateFilter.FilterOrder, stateFilter.Order);
            Assert.True(stateFilter.Order > retryFilter.Order);
        }
        finally
        {
            RemoveAddedFilters(before);
        }
    }

    [Fact]
    public void BuildDispatcherOnly_DoesNotRegisterDeduplicationCleanupFilter()
    {
        var before = GlobalJobFilters.Filters.Select(f => f.Instance).ToList();
        var setup = new HangfireSetup();
        setup.Configure(opts => { });
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        try
        {
            setup.BuildDispatcherOnly(Substitute.For<IBackgroundJobClient>(), storage);

            Assert.Empty(GlobalJobFilters.Filters.Select(f => f.Instance).Except(before));
        }
        finally
        {
            RemoveAddedFilters(before);
        }
    }

    private static void RemoveAddedFilters(List<object> before)
    {
        foreach (var instance in GlobalJobFilters.Filters.Select(f => f.Instance).Except(before).ToList())
            GlobalJobFilters.Filters.Remove(instance);
    }
}
