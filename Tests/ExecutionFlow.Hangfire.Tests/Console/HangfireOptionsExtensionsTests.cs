using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Console;
using Hangfire.Console;

namespace ExecutionFlow.Hangfire.Tests.Console;

public class HangfireOptionsExtensionsTests
{
    [Fact]
    public void ConfigureConsole_AddsLoggerFactory()
    {
        var options = new HangfireOptions();

        options.ConfigureConsole();

        Assert.Single(options.LoggerFactoryTypes);
    }

    [Fact]
    public void ConfigureConsole_AddsOption()
    {
        var options = new HangfireOptions();

        options.ConfigureConsole();

        Assert.Single(options.OptionValues);
    }

    [Fact]
    public void ConfigureConsole_WithAction_AppliesConfiguration()
    {
        var options = new HangfireOptions();

        options.ConfigureConsole(config =>
        {
            config.SetColor(HandlerLogType.Error, ConsoleTextColor.Blue);
        });

        Assert.Single(options.LoggerFactoryTypes);
        Assert.Single(options.OptionValues);
    }

    [Fact]
    public void ConfigureConsole_WithAction_Throws_ForNullAction()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.ConfigureConsole(null!));
    }

    [Fact]
    public void ConfigureConsole_ReturnsOptions_ForChaining()
    {
        var options = new HangfireOptions();

        var result = options.ConfigureConsole();

        Assert.Same(options, result);
    }
}
