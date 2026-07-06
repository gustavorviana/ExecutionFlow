using Hangfire;
using Hangfire.Common;
using NSubstitute;
using HangfireJobDispatcher = ExecutionFlow.Hangfire.Infrastructure.HangfireJobDispatcher;

namespace ExecutionFlow.Hangfire.Tests;

public class DashboardOptionsExtensionsTests
{
    private static Job CreateEventJob() =>
        Job.FromExpression<HangfireJobDispatcher>(x => x.DispatchEventAsync<TestEvent>(default!, null, null!, default));

    [Fact]
    public void UseExecutionFlowJobNames_WithInstance_SetsDisplayNameFunc()
    {
        var jobName = Substitute.For<IHangfireJobName>();
        jobName.GetName(Arg.Any<Job>()).Returns("resolved-name");
        var options = new DashboardOptions();

        options.UseExecutionFlowJobNames(jobName);

        Assert.NotNull(options.DisplayNameFunc);
        Assert.Equal("resolved-name", options.DisplayNameFunc(null!, CreateEventJob()));
    }

    [Fact]
    public void UseExecutionFlowJobNames_WithServiceProvider_ResolvesLazily()
    {
        var jobName = Substitute.For<IHangfireJobName>();
        jobName.GetName(Arg.Any<Job>()).Returns("from-provider");
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IHangfireJobName)).Returns(jobName);
        var options = new DashboardOptions();

        options.UseExecutionFlowJobNames(provider);

        Assert.Equal("from-provider", options.DisplayNameFunc(null!, CreateEventJob()));
    }

    [Fact]
    public void UseExecutionFlowJobNames_WithServiceProvider_Throws_WhenNotRegistered()
    {
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IHangfireJobName)).Returns(null);
        var options = new DashboardOptions();

        options.UseExecutionFlowJobNames(provider);

        Assert.Throws<InvalidOperationException>(() => options.DisplayNameFunc(null!, CreateEventJob()));
    }

    [Fact]
    public void UseExecutionFlowJobNames_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DashboardOptionsExtensions.UseExecutionFlowJobNames(null!, Substitute.For<IHangfireJobName>()));
    }

    public class TestEvent { }
}
