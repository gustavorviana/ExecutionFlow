using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using System.ComponentModel;

namespace ExecutionFlow.Tests.Abstractions;

/// <summary>014-handler-configuration: configuration classes, the builder, and item-by-item precedence in the plan.</summary>
public class HandlerConfigurationTests
{
    // --- FR-001: configuration class and builder ---

    [Fact]
    public void Apply_SetsEveryValue()
    {
        var planner = new ExecutionPlanner();
        planner.Add<PrerequisiteHandler>();
        planner.ApplyConfiguration(new FullConfiguration());

        var step = planner.Build().Get<PlainHandler>();

        Assert.Equal("*/10 * * * *", step.Cron);
        Assert.Null(step.Interval);
        Assert.Equal("plain", step.Id);
        Assert.Equal("America/Sao_Paulo", step.TimeZone);
        Assert.Equal("Plain", step.DisplayName);
        Assert.Equal(new[] { typeof(PrerequisiteHandler) }, step.Prerequisites);
        Assert.Equal(TimeSpan.FromMinutes(5), step.MinInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), step.OverlapTimeout);
        Assert.False(step.ConfiguredEnabled);
        Assert.Equal("value", step.Settings["Test:Setting"]);
    }

    [Fact]
    public void Interval_ReplacesAttributeCron()
    {
        var planner = new ExecutionPlanner();
        planner.Add<CronAttributeHandler>().Interval(TimeSpan.FromMinutes(90));

        var step = planner.Build().Get<CronAttributeHandler>();

        Assert.Equal(TimeSpan.FromMinutes(90), step.Interval);
        Assert.Null(step.Cron);
    }

    [Fact]
    public void Cron_ReplacesInterval_AndTheReverse()
    {
        var builder = new ExecutionPlanner().Add<PlainHandler>();

        builder.Interval(TimeSpan.FromMinutes(5)).Cron("0 * * * *");
        Assert.Equal("0 * * * *", builder.ConfiguredCron);
        Assert.Null(builder.ConfiguredInterval);

        builder.Interval(TimeSpan.FromMinutes(5));
        Assert.Null(builder.ConfiguredCron);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TimeZone_Throws_WhenNullOrEmpty(string? timeZone)
    {
        var builder = new ExecutionPlanner().Add<PlainHandler>();

        var ex = Assert.Throws<ArgumentException>(() => builder.TimeZone(timeZone!));

        Assert.Contains(typeof(PlainHandler).FullName!, ex.Message);
    }

    [Fact]
    public void Apply_Throws_WhenTypeIsNotRecurringHandler()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ExecutionPlanner().Add(typeof(string)));

        Assert.Contains(typeof(string).FullName!, ex.Message);
    }

    [Fact]
    public void Apply_Throws_WhenHandlerConfiguredTwice()
    {
        var twoClasses = new ExecutionPlanner();
        twoClasses.ApplyConfiguration(new FullConfiguration());

        var classAndAdd = new ExecutionPlanner();
        classAndAdd.Add<PlainHandler>();

        Assert.Contains(typeof(PlainHandler).FullName!,
            Assert.Throws<InvalidOperationException>(() => twoClasses.ApplyConfiguration(new FullConfiguration())).Message);
        Assert.Contains(typeof(PlainHandler).FullName!,
            Assert.Throws<InvalidOperationException>(() => classAndAdd.ApplyConfiguration(new FullConfiguration())).Message);
    }

    [Fact]
    public void InlineAdd_ReturnsHandlerBuilder()
    {
        HandlerBuilder<PlainHandler> builder = new ExecutionPlanner().Add<PlainHandler>();

        builder.Cron("*/5 * * * *").Id("inline").TimeZone("UTC").NoOverlap(TimeSpan.FromMinutes(1));

        Assert.Equal("inline", builder.ConfiguredId);
    }

    // --- FR-002: precedence per item ---

    [Fact]
    public void Builder_ChangesOnlyWhatItSets()
    {
        var planner = new ExecutionPlanner();
        planner.Add<DecoratedHandler>().NoOverlap(TimeSpan.FromMinutes(10));

        var step = planner.Build().Get<DecoratedHandler>();

        // Cron, Id and name aren't set by the builder: the provider keeps the attribute's.
        Assert.Null(step.Cron);
        Assert.Null(step.Id);
        Assert.Equal("Decorated", step.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(10), step.OverlapTimeout);
    }

    [Fact]
    public void Dependencies_Merge()
    {
        var planner = new ExecutionPlanner();
        planner.Add<PrerequisiteHandler>();
        planner.Add<OtherPrerequisiteHandler>();
        planner.Add<AttributeDependentHandler>().DependsOn<OtherPrerequisiteHandler>();

        Assert.Equal(new[] { typeof(PrerequisiteHandler), typeof(OtherPrerequisiteHandler) },
            planner.Build().Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void RemoveDependency_DropsAttributeDependency()
    {
        var planner = new ExecutionPlanner();
        planner.Add<AttributeDependentHandler>().RemoveDependency<PrerequisiteHandler>();

        Assert.Empty(planner.Build().Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void ClearDependencies_DropsAll()
    {
        var planner = new ExecutionPlanner();
        planner.Add<OtherPrerequisiteHandler>();
        planner.Add<AttributeDependentHandler>().DependsOn<OtherPrerequisiteHandler>().ClearDependencies();

        Assert.Empty(planner.Build().Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void RemoveDependency_IsNoOp_WhenNotDeclared()
    {
        var planner = new ExecutionPlanner();
        planner.Add<PrerequisiteHandler>();
        planner.Add<AttributeDependentHandler>().RemoveDependency<OtherPrerequisiteHandler>();

        Assert.Equal(new[] { typeof(PrerequisiteHandler) }, planner.Build().Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void ClearThenDependsOn_KeepsOnlyNewDependency()
    {
        var planner = new ExecutionPlanner();
        planner.Add<OtherPrerequisiteHandler>();
        planner.Add<AttributeDependentHandler>().ClearDependencies().DependsOn<OtherPrerequisiteHandler>();

        Assert.Equal(new[] { typeof(OtherPrerequisiteHandler) }, planner.Build().Get<AttributeDependentHandler>().Prerequisites);
    }

    [Fact]
    public void AllowOverlap_TurnsOverlapBackOn()
    {
        var planner = new ExecutionPlanner();
        planner.Add<DecoratedHandler>().AllowOverlap();

        Assert.Null(planner.Build().Get<DecoratedHandler>().OverlapTimeout);
    }

    // --- FR-003: applying configurations ---

    [Fact]
    public void ApplyFromAssembly_AppliesEveryConfiguration()
    {
        var planner = new ExecutionPlanner();
        planner.ApplyConfigurationsFromAssembly(typeof(ScanTarget.ScanAConfiguration).Assembly, InNamespace(typeof(ScanTarget.ScanAConfiguration)));

        var plan = planner.Build();

        Assert.Equal("scan-a", plan.Get<ScanTarget.ScanAHandler>().Id);
        Assert.Equal("scan-b", plan.Get<ScanTarget.ScanBHandler>().Id);
        Assert.Equal("scan-c", plan.Get<ScanTarget.ScanCHandler>().Id);
    }

    [Fact]
    public void ApplyFromAssembly_SkipsAbstract_ThrowsWithoutParameterlessConstructor()
    {
        // The scanned assembly has an abstract configuration (skipped above); one without a parameterless constructor throws.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ExecutionPlanner().ApplyConfigurationsFromAssembly(typeof(NoDefaultConstructorTarget.Marker).Assembly, InNamespace(typeof(NoDefaultConstructorTarget.Marker))));

        Assert.Contains(nameof(NoDefaultConstructorTarget.NoDefaultConstructorConfiguration), ex.Message);
    }

    [Fact]
    public void ApplyFromAssembly_CoexistsWithInlineAdd()
    {
        var planner = new ExecutionPlanner();
        planner.ApplyConfigurationsFromAssembly(typeof(ScanTarget.ScanAConfiguration).Assembly, InNamespace(typeof(ScanTarget.ScanAConfiguration)));
        planner.Add<PlainHandler>().Id("inline");

        var plan = planner.Build();

        Assert.Equal("scan-a", plan.Get<ScanTarget.ScanAHandler>().Id);
        Assert.Equal("inline", plan.Get<PlainHandler>().Id);
    }

    private static Func<Type, bool> InNamespace(Type marker) => t => t.Namespace == marker.Namespace;

    // Test types

    private abstract class TestHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class PlainHandler : TestHandler { }
    private class PrerequisiteHandler : TestHandler { }
    private class OtherPrerequisiteHandler : TestHandler { }

    [Recurring("*/10 * * * *")] private class CronAttributeHandler : TestHandler { }

    [Recurring("*/10 * * * *", Id = "a"), NoOverlap(300), DisplayName("Decorated")]
    private class DecoratedHandler : TestHandler { }

    [DependsOn(typeof(PrerequisiteHandler))] private class AttributeDependentHandler : TestHandler { }

    private class FullConfiguration : IHandlerConfiguration<PlainHandler>
    {
        public void Configure(HandlerBuilder<PlainHandler> handler)
        {
            handler.Cron("*/10 * * * *")
                .Id("plain")
                .TimeZone("America/Sao_Paulo")
                .DisplayName("Plain")
                .DependsOn<PrerequisiteHandler>()
                .RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(5))
                .NoOverlap(TimeSpan.FromMinutes(2))
                .Enabled(false)
                .HasSetting("Test:Setting", "value");
        }
    }
}
