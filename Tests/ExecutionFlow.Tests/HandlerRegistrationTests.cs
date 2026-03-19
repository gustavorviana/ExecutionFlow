using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Tests;

public class HandlerRegistrationTests
{
    [Fact]
    public void IsRecurring_True_WhenEventTypeIsNull()
    {
        var registration = new HandlerRegistration(
            handlerType: typeof(object),
            eventType: null,
            displayName: "Test",
            cron: "* * * * *");

        Assert.True(registration.IsRecurring);
    }

    [Fact]
    public void IsRecurring_False_WhenEventTypeIsProvided()
    {
        var registration = new HandlerRegistration(
            handlerType: typeof(object),
            eventType: typeof(string),
            displayName: "Test",
            cron: null);

        Assert.False(registration.IsRecurring);
    }

    [Fact]
    public void Properties_StoreCorrectValues()
    {
        var handlerType = typeof(HandlerRegistrationTests);
        var eventType = typeof(string);

        var registration = new HandlerRegistration(
            handlerType: handlerType,
            eventType: eventType,
            displayName: "My Handler",
            cron: "0 */5 * * *");

        Assert.Equal(handlerType, registration.HandlerType);
        Assert.Equal(eventType, registration.EventType);
        Assert.Equal("My Handler", registration.DisplayName);
        Assert.Equal("0 */5 * * *", registration.Cron);
    }

    [Fact]
    public void Cron_IsNull_ForNonRecurringHandler()
    {
        var registration = new HandlerRegistration(
            handlerType: typeof(object),
            eventType: typeof(string),
            displayName: "Test",
            cron: null);

        Assert.Null(registration.Cron);
    }
}
