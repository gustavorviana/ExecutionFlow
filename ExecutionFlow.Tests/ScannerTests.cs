using System.ComponentModel;
using System.Reflection;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Scanner;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class ScannerTests
{
    [Fact]
    public void Scanning_Finds_IHandler_Implementations()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.FirstOrDefault(r => r.HandlerType == typeof(SimpleRecurringHandler));
        Assert.NotNull(reg);
        Assert.Equal(typeof(IHandler), reg.ServiceType);
        Assert.Null(reg.JobType);
    }

    [Fact]
    public void Scanning_Finds_IHandler_TEvent_Implementations()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.FirstOrDefault(r => r.HandlerType == typeof(EventHandler));
        Assert.NotNull(reg);
        Assert.Equal(typeof(IHandler<SampleEvent>), reg.ServiceType);
        Assert.Equal(typeof(SampleEvent), reg.JobType);
    }

    [Fact]
    public void Reads_Recurring_Attribute_Correctly()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.First(r => r.HandlerType == typeof(SimpleRecurringHandler));
        Assert.True(reg.IsRecurring);
        Assert.Equal("0 * * * *", reg.Cron);
    }

    [Fact]
    public void Non_Recurring_Handler_Has_No_Cron()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.First(r => r.HandlerType == typeof(EventHandler));
        Assert.False(reg.IsRecurring);
        Assert.Null(reg.Cron);
    }

    [Fact]
    public void Reads_DisplayName_Attribute()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.First(r => r.HandlerType == typeof(SimpleRecurringHandler));
        Assert.Equal("My Recurring Job", reg.DisplayName);
    }

    [Fact]
    public void Falls_Back_To_Class_Name_When_No_DisplayName()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        var reg = registrations.First(r => r.HandlerType == typeof(EventHandler));
        Assert.Equal(nameof(EventHandler), reg.DisplayName);
    }

    [Fact]
    public void Ignores_Abstract_Classes()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        Assert.DoesNotContain(registrations, r => r.HandlerType == typeof(AbstractHandler));
    }

    [Fact]
    public void Ignores_Interfaces()
    {
        var registrations = ExecutionFlowScanner.ScanAssembly(typeof(ScannerTests).Assembly);

        // IHandler itself should not be registered
        Assert.DoesNotContain(registrations, r => r.HandlerType == typeof(IHandler));
        Assert.DoesNotContain(registrations, r => r.HandlerType == typeof(IHandler<>));
    }

    [Fact]
    public void Handles_ReflectionTypeLoadException_Gracefully()
    {
        // Create a mock assembly that throws ReflectionTypeLoadException
        var assembly = Substitute.For<Assembly>();
        var types = new Type[] { typeof(EventHandler), null! };
        var exception = new ReflectionTypeLoadException(types, new Exception[] { new Exception("load error") });
        assembly.GetTypes().Returns(_ => throw exception);

        var registrations = ExecutionFlowScanner.ScanAssembly(assembly);

        // Should still find EventHandler despite the exception
        Assert.Contains(registrations, r => r.HandlerType == typeof(EventHandler));
    }

    // Test handler types

    public class SampleEvent { }

    [Recurring("0 * * * *")]
    [DisplayName("My Recurring Job")]
    public class SimpleRecurringHandler : IHandler
    {
        public Task HandleAsync(Abstractions.ExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class EventHandler : IHandler<SampleEvent>
    {
        public Task HandleAsync(ExecutionContext<SampleEvent> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public abstract class AbstractHandler : IHandler
    {
        public abstract Task HandleAsync(Abstractions.ExecutionContext context, CancellationToken cancellationToken);
    }
}
