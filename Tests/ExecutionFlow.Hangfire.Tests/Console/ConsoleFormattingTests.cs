using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Console;

namespace ExecutionFlow.Hangfire.Tests.Console;

/// <summary>logging-and-console to-be items 4, 5 and 7: ILogger-style templates, minimum level, progress bars.</summary>
public class ConsoleFormattingTests
{
    private static string Format(string message, params object[] args) =>
        new ConsoleConfig().FormatMessage(HandlerLogType.Information, message, args);

    [Fact]
    public void FormatMessage_FillsNamedPlaceholders_ByPosition()
    {
        Assert.Equal("[INFORMATION] Order 42 paid by Ana", Format("Order {OrderId} paid by {Customer}", 42, "Ana"));
    }

    [Fact]
    public void FormatMessage_KeepsNumericPlaceholdersWorking()
    {
        Assert.Equal("[INFORMATION] 1 then 2", Format("{0} then {1}", 1, 2));
    }

    [Fact]
    public void FormatMessage_AppliesFormatAndAlignment()
    {
        var formatted = new ConsoleConfig().FormatMessage(HandlerLogType.Information, "[{Value,6:0.0}]", new object[] { 3.14159 });

        Assert.Equal($"[INFORMATION] [{3.1.ToString("0.0").PadLeft(6)}]", formatted);
    }

    [Fact]
    public void FormatMessage_TreatsDoubledBracesAsLiterals()
    {
        Assert.Equal("[INFORMATION] {literal} 7", Format("{{literal}} {Count}", 7));
    }

    [Fact]
    public void FormatMessage_KeepsPlaceholder_WhenArgumentIsMissing()
    {
        Assert.Equal("[INFORMATION] a {Missing}", Format("{First} {Missing}", "a"));
    }

    [Fact]
    public void FormatMessage_WithoutArguments_WritesMessageAsIs()
    {
        Assert.Equal("[INFORMATION] {NotATemplate}", Format("{NotATemplate}"));
    }

    [Fact]
    public void FormatMessage_UsesCustomFormatter_WhenSet()
    {
        var config = new ConsoleConfig { Formatter = (level, message, args) => "custom" };

        Assert.Equal("custom", config.FormatMessage(HandlerLogType.Error, "x", Array.Empty<object>()));
    }

    [Theory]
    [InlineData(HandlerLogType.Trace, HandlerLogType.Trace, true)]
    [InlineData(HandlerLogType.Warning, HandlerLogType.Information, false)]
    [InlineData(HandlerLogType.Warning, HandlerLogType.Error, true)]
    [InlineData(HandlerLogType.Information, HandlerLogType.Success, true)]
    [InlineData(HandlerLogType.Warning, HandlerLogType.Success, false)]
    public void IsEnabled_RespectsMinimumLevel_WithSuccessAsInformation(HandlerLogType minimum, HandlerLogType level, bool expected)
    {
        var config = new ConsoleConfig { MinimumLevel = minimum };

        Assert.Equal(expected, config.IsEnabled(level));
    }

    [Fact]
    public void MinimumLevel_DefaultsToTrace()
    {
        Assert.Equal(HandlerLogType.Trace, new ConsoleConfig().MinimumLevel);
    }

    [Fact]
    public void CreateProgressBar_Throws_OutsideAHangfireJob()
    {
        var context = new FlowContextBuilder(new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>())).Build();

        Assert.Throws<InvalidOperationException>(() => context.CreateProgressBar("x"));
    }
}
