using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Console;
using Hangfire.Console;

namespace ExecutionFlow.Hangfire.Tests.Console;

public class ConsoleConfigTests
{
    [Fact]
    public void GetColor_ReturnsDefaultColor_ForEachLogType()
    {
        var config = new ConsoleConfig();

        Assert.Equal(ConsoleTextColor.DarkGray, config.GetColor(HandlerLogType.Trace));
        Assert.Equal(ConsoleTextColor.Gray, config.GetColor(HandlerLogType.Debug));
        Assert.Equal(ConsoleTextColor.White, config.GetColor(HandlerLogType.Information));
        Assert.Equal(ConsoleTextColor.Yellow, config.GetColor(HandlerLogType.Warning));
        Assert.Equal(ConsoleTextColor.Red, config.GetColor(HandlerLogType.Error));
        Assert.Equal(ConsoleTextColor.DarkRed, config.GetColor(HandlerLogType.Critical));
        Assert.Equal(ConsoleTextColor.Green, config.GetColor(HandlerLogType.Success));
    }

    [Fact]
    public void SetColor_OverridesDefaultColor()
    {
        var config = new ConsoleConfig();

        config.SetColor(HandlerLogType.Error, ConsoleTextColor.Magenta);

        Assert.Equal(ConsoleTextColor.Magenta, config.GetColor(HandlerLogType.Error));
    }

    [Fact]
    public void GetColor_ReturnsWhite_ForUnmappedLogType()
    {
        var config = new ConsoleConfig();

        var color = config.GetColor((HandlerLogType)999);

        Assert.Equal(ConsoleTextColor.White, color);
    }

    [Fact]
    public void Formatter_IsNullByDefault()
    {
        var config = new ConsoleConfig();

        Assert.Null(config.Formatter);
    }

    [Fact]
    public void Formatter_CanBeSet()
    {
        var config = new ConsoleConfig();

        config.Formatter = (level, msg, args) => $"CUSTOM: {msg}";

        Assert.NotNull(config.Formatter);
        Assert.Equal("CUSTOM: hello", config.Formatter(HandlerLogType.Information, "hello", null));
    }
}
