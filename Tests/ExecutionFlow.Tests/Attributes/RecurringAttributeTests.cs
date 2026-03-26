using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests.Attributes;

public class RecurringAttributeTests
{
    [Fact]
    public void Constructor_StoresCron()
    {
        var attr = new RecurringAttribute("*/5 * * * *");

        Assert.Equal("*/5 * * * *", attr.Cron);
    }

    [Fact]
    public void Cron_CanBeRetrievedFromType()
    {
        var attr = typeof(DecoratedHandler).GetCustomAttributes(typeof(RecurringAttribute), false)
            .Cast<RecurringAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("0 * * * *", attr!.Cron);
    }

    [Fact]
    public void AttributeUsage_AllowsSingleOnClass()
    {
        var usage = typeof(RecurringAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Recurring("0 * * * *")]
    private class DecoratedHandler { }
}
