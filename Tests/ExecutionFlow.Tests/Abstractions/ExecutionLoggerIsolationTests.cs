using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests.Abstractions;

/// <summary>logging-and-console to-be items 1–3: isolation, logger context, no duplicate factories.</summary>
public class ExecutionLoggerIsolationTests
{
    private static FlowParameters Parameters() => new FlowParameters();

    [Fact]
    public void ThrowingLogger_DoesNotStopOtherLoggers_NorThrowToTheHandler()
    {
        var throwing = Substitute.For<IExecutionLogger>();
        throwing.When(l => l.Log(Arg.Any<HandlerLogType>(), Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(_ => throw new InvalidOperationException("logger bug"));
        var working = Substitute.For<IExecutionLogger>();

        var factory = new ExecutionLoggerFactory(new[] { FactoryReturning(throwing), FactoryReturning(working) });
        var logger = factory.CreateLogger(Parameters());

        logger.Info("hello");

        working.Received(1).Log(HandlerLogType.Information, "hello");
    }

    [Fact]
    public void ThrowingFactory_IsSkipped_WhenCreatingLoggers()
    {
        var broken = Substitute.For<IExecutionLoggerFactory>();
        broken.CreateLogger(Arg.Any<FlowParameters>()).Returns(_ => throw new InvalidOperationException("factory bug"));
        var working = Substitute.For<IExecutionLogger>();

        var logger = new ExecutionLoggerFactory(new[] { broken, FactoryReturning(working) }).CreateLogger(Parameters());
        logger.Warning("careful");

        working.Received(1).Log(HandlerLogType.Warning, "careful");
    }

    [Fact]
    public void ContextFactory_ReceivesJobAndHandlerFacts()
    {
        var contextFactory = Substitute.For<IExecutionLoggerContextFactory>();
        ExecutionLoggerContext? received = null;
        contextFactory.CreateLogger(Arg.Do<ExecutionLoggerContext>(c => received = c)).Returns(Substitute.For<IExecutionLogger>());

        new FlowContextBuilder(new ExecutionLoggerFactory(new IExecutionLoggerFactory[] { contextFactory }))
            .SetJob("job-9", 2)
            .SetHandler(typeof(SampleHandler), typeof(SampleEvent))
            .Build(new SampleEvent(), _ => { });

        Assert.NotNull(received);
        Assert.Equal("job-9", received!.JobId);
        Assert.Equal(2, received.AttemptNumber);
        Assert.Equal(typeof(SampleHandler), received.HandlerType);
        Assert.Equal(typeof(SampleEvent), received.EventType);
        contextFactory.DidNotReceiveWithAnyArgs().CreateLogger(default(FlowParameters)!);
    }

    [Fact]
    public void PlainFactory_KeepsReceivingParameters()
    {
        var plain = Substitute.For<IExecutionLoggerFactory>();
        var builder = new FlowContextBuilder(new ExecutionLoggerFactory(new[] { plain }));

        var context = builder.SetJob("job-1", 1).Build();

        plain.Received(1).CreateLogger(context.Parameters);
    }

    [Fact]
    public void AddLogger_IgnoresTheSameFactoryRegisteredTwice()
    {
        var options = new TestOptions();

        options.AddLogger<SampleLoggerFactory>();
        options.AddLogger<SampleLoggerFactory>();

        Assert.Single(options.LoggerFactoryTypes);
    }

    private static IExecutionLoggerFactory FactoryReturning(IExecutionLogger logger)
    {
        var factory = Substitute.For<IExecutionLoggerFactory>();
        factory.CreateLogger(Arg.Any<FlowParameters>()).Returns(logger);
        return factory;
    }

    public class TestOptions : ExecutionFlowOptions { }

    public class SampleEvent { }

    public abstract class SampleHandler : IHandler<SampleEvent>
    {
        public abstract Task HandleAsync(FlowContext<SampleEvent> context, CancellationToken cancellationToken);
    }

    public class SampleLoggerFactory : IExecutionLoggerFactory
    {
        public IExecutionLogger CreateLogger(FlowParameters parameters) => null!;
    }
}
