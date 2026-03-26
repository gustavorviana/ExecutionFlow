using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class ValidationTests
{
    private class TestOptions : ExecutionFlowOptions { }

    // --- ExecutionFlowOptions.Add() Validations ---

    [Fact]
    public void Add_Throws_ForNullType()
    {
        var options = new TestOptions();

        Assert.Throws<ArgumentNullException>(() => options.Add(null!));
    }

    [Fact]
    public void Add_Throws_ForTypeThatDoesNotImplementIHandler()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.Add(typeof(NonHandlerClass)));
        Assert.Contains("does not implement IHandler", ex.Message);
    }

    [Fact]
    public void Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(typeof(DualHandler)));
        Assert.Contains("implements both IHandler and IHandler<TEvent>", ex.Message);
    }

    [Fact]
    public void Add_Throws_WhenTypeImplementsMultipleGenericIHandlers()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(typeof(MultiEventHandler)));
        Assert.Contains("multiple event types", ex.Message);
    }

    [Fact]
    public void Add_Throws_WhenDuplicateEventHandler_ForSameEventType()
    {
        var options = new TestOptions();
        options.Add(typeof(EventHandlerA));

        var ex = Assert.Throws<InvalidOperationException>(() => options.Add(typeof(EventHandlerACopy)));
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void Add_AllowsReregistering_SameHandler()
    {
        var options = new TestOptions();
        options.Add(typeof(EventHandlerA));

        var exception = Record.Exception(() => options.Add(typeof(EventHandlerA)));

        Assert.Null(exception);
    }

    // --- Scan Validations ---

    [Fact]
    public void Scan_IgnoresNonHandlerTypes()
    {
        var options = new TestOptions();

        options.Scan(typeof(ValidationTests).Assembly);

        Assert.DoesNotContain(options.EventHandlers.Values, e => e.HandlerType == typeof(NonHandlerClass));
        Assert.DoesNotContain(options.RecurringHandlers.Values, r => r.HandlerType == typeof(NonHandlerClass));
    }

    // --- AddLogger Validations ---

    [Fact]
    public void AddLogger_Throws_ForNullType()
    {
        var options = new TestOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddLogger(null!));
    }

    [Fact]
    public void AddLogger_Throws_ForTypeThatDoesNotImplementIExecutionLoggerFactory()
    {
        var options = new TestOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.AddLogger(typeof(string)));
        Assert.Contains("does not implement IExecutionLoggerFactory", ex.Message);
    }

    [Fact]
    public void AddLogger_Adds_ValidFactoryType()
    {
        var options = new TestOptions();

        options.AddLogger(typeof(TestLoggerFactory));

        Assert.Single(options.LoggerFactoryTypes);
        Assert.Equal(typeof(TestLoggerFactory), options.LoggerFactoryTypes[0]);
    }

    // --- RecurringJobRegistryInfo Validations ---

    [Fact]
    public void RecurringJobRegistryInfo_Throws_ForNullHandlerType()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecurringJobRegistryInfo(null!, "name", "* * * * *"));
    }

    // --- EventJobRegistryInfo Validations ---

    [Fact]
    public void EventJobRegistryInfo_Throws_ForNullHandlerType()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventJobRegistryInfo(null!, typeof(string), "name"));
    }

    [Fact]
    public void EventJobRegistryInfo_Throws_ForNullEventType()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventJobRegistryInfo(typeof(object), null!, "name"));
    }

    // --- ExecutionLoggerFactory Validations ---

    [Fact]
    public void ExecutionLoggerFactory_Throws_ForNullFactories()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionLoggerFactory(null!));
    }

    [Fact]
    public void ExecutionLoggerFactory_CreatesLogger_WithEmptyFactories()
    {
        var factory = new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>());

        var logger = factory.CreateLogger(jobParameters: new FlowParameters());

        Assert.NotNull(logger);
    }

    // --- FlowContextBuilder Validations ---

    [Fact]
    public void FlowContextBuilder_Throws_ForNullLogFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FlowContextBuilder(null!));
    }

    // --- CompositeExecutionLogger ---

    [Fact]
    public void CompositeExecutionLogger_DispatchesToAllLoggers()
    {
        var logger1 = Substitute.For<IExecutionLogger>();
        var logger2 = Substitute.For<IExecutionLogger>();
        var factory1 = Substitute.For<IExecutionLoggerFactory>();
        var factory2 = Substitute.For<IExecutionLoggerFactory>();

        factory1.CreateLogger(default!).ReturnsForAnyArgs(logger1);
        factory2.CreateLogger(default!).ReturnsForAnyArgs(logger2);

        var loggerFactory = new ExecutionLoggerFactory(new[] { factory1, factory2 });
        var composite = loggerFactory.CreateLogger(new FlowParameters());

        composite.Log(HandlerLogType.Information, "test message");

        logger1.Received(1).Log(HandlerLogType.Information, "test message");
        logger2.Received(1).Log(HandlerLogType.Information, "test message");
    }

    [Fact]
    public void CompositeExecutionLogger_SkipsNullLoggers()
    {
        var logger1 = Substitute.For<IExecutionLogger>();
        var factory1 = Substitute.For<IExecutionLoggerFactory>();
        var factoryNull = Substitute.For<IExecutionLoggerFactory>();

        factory1.CreateLogger(default!).ReturnsForAnyArgs(logger1);
        factoryNull.CreateLogger(default!).ReturnsForAnyArgs((IExecutionLogger)null!);

        var loggerFactory = new ExecutionLoggerFactory(new[] { factoryNull, factory1 });
        var composite = loggerFactory.CreateLogger(new FlowParameters());

        composite.Log(HandlerLogType.Information, "test");

        logger1.Received(1).Log(HandlerLogType.Information, "test");
    }

    // Test types

    public class NonHandlerClass { }

    public class TestEvent { }
    public class TestEvent2 { }

    // Abstract to prevent Scan() from picking them up
    public abstract class DualHandler : IHandler, IHandler<TestEvent>
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public abstract class MultiEventHandler : IHandler<TestEvent>, IHandler<TestEvent2>
    {
        public Task HandleAsync(FlowContext<TestEvent> context, CancellationToken ct) => Task.CompletedTask;
        public Task HandleAsync(FlowContext<TestEvent2> context, CancellationToken ct) => Task.CompletedTask;
    }

    public class DuplicateTestEvent { }

    public class EventHandlerA : IHandler<DuplicateTestEvent>
    {
        public Task HandleAsync(FlowContext<DuplicateTestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public abstract class EventHandlerB : IHandler<DuplicateTestEvent>
    {
        public Task HandleAsync(FlowContext<DuplicateTestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public abstract class EventHandlerACopy : IHandler<DuplicateTestEvent>
    {
        public Task HandleAsync(FlowContext<DuplicateTestEvent> context, CancellationToken ct) => Task.CompletedTask;
    }

    public class TestLoggerFactory : IExecutionLoggerFactory
    {
        public IExecutionLogger CreateLogger(FlowParameters parameters) => null!;
    }
}
