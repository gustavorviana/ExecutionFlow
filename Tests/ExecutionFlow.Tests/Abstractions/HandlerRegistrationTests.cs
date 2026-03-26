using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Tests.Abstractions;

public class HandlerRegistrationTests
{
    [Fact]
    public void EventJobRegistryInfo_Stores_Properties()
    {
        var handlerType = typeof(HandlerRegistrationTests);
        var eventType = typeof(string);

        var registration = new EventJobRegistryInfo(handlerType, eventType, "My Handler");

        Assert.Equal(handlerType, registration.HandlerType);
        Assert.Equal(eventType, registration.EventType);
        Assert.Equal("My Handler", registration.DisplayName);
    }

    [Fact]
    public void EventJobRegistryInfo_Implements_IJobRegistryInfo()
    {
        var registration = new EventJobRegistryInfo(typeof(object), typeof(string), "Test");

        Assert.IsAssignableFrom<IJobRegistryInfo>(registration);
    }

    [Fact]
    public void RecurringJobRegistryInfo_Stores_Properties()
    {
        var handlerType = typeof(HandlerRegistrationTests);

        var registration = new RecurringJobRegistryInfo(handlerType, "Recurring Job", "0 */5 * * *");

        Assert.Equal(handlerType, registration.HandlerType);
        Assert.Equal("Recurring Job", registration.DisplayName);
        Assert.Equal("0 */5 * * *", registration.Cron);
    }

    [Fact]
    public void RecurringJobRegistryInfo_Implements_IJobRegistryInfo()
    {
        var registration = new RecurringJobRegistryInfo(typeof(object), "Test", "* * * * *");

        Assert.IsAssignableFrom<IJobRegistryInfo>(registration);
    }

    [Fact]
    public void RecurringJobRegistryInfo_Cron_CanBeNull()
    {
        var registration = new RecurringJobRegistryInfo(typeof(object), "Test", null);

        Assert.Null(registration.Cron);
    }
}
