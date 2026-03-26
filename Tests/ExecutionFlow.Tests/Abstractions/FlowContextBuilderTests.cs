using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests.Abstractions;

public class FlowContextBuilderTests
{
    private static ExecutionLoggerFactory CreateLoggerFactory()
    {
        return new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>());
    }

    [Fact]
    public void Build_NonGeneric_ReturnsFlowContext()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());

        var context = builder.Build();

        Assert.NotNull(context);
        Assert.NotNull(context.Log);
        Assert.NotNull(context.Parameters);
    }

    [Fact]
    public void Build_Generic_ReturnsFlowContextWithEvent()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        var evt = new TestEvent { Name = "test" };

        var context = builder.Build(evt, _ => { });

        Assert.NotNull(context);
        Assert.Equal("test", context.Event.Name);
    }

    [Fact]
    public void Build_Generic_InvokesOnCustomIdChange()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        string? capturedId = null;

        var context = builder.Build(new TestEvent(), id => capturedId = id);
        context.SetCustomId("my-id");

        Assert.Equal("my-id", capturedId);
    }

    [Fact]
    public void Parameters_ArePassedToContext()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Add("key1", "value1");

        var context = builder.Build();

        Assert.True(context.Parameters.ContainsKey("key1"));
        Assert.Equal("value1", context.Parameters["key1"]);
    }

    [Fact]
    public void Build_Generic_CreatesLogger()
    {
        var mockLogger = Substitute.For<IExecutionLogger>();
        var mockFactory = Substitute.For<IExecutionLoggerFactory>();
        mockFactory.CreateLogger(Arg.Any<IDictionary<string, object>>()).Returns(mockLogger);

        var loggerFactory = new ExecutionLoggerFactory(new[] { mockFactory });
        var builder = new FlowContextBuilder(loggerFactory);

        var context = builder.Build();

        Assert.NotNull(context.Log);
    }

    // --- ThrowIfBuilt tests ---

    [Fact]
    public void Build_NonGeneric_Throws_WhenCalledTwice()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Generic_Throws_WhenCalledTwice()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build(new TestEvent(), _ => { });

        Assert.Throws<InvalidOperationException>(() => builder.Build(new TestEvent(), _ => { }));
    }

    [Fact]
    public void Build_Generic_Throws_AfterNonGenericBuild()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build(new TestEvent(), _ => { }));
    }

    [Fact]
    public void Build_NonGeneric_Throws_AfterGenericBuild()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build(new TestEvent(), _ => { });

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Add_Throws_AfterBuild()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Add("key", "value"));
    }

    [Fact]
    public void AddReadOnly_Throws_AfterBuild()
    {
        var builder = new FlowContextBuilder(CreateLoggerFactory());
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.AddReadOnly("key", "value"));
    }

    public class TestEvent
    {
        public string Name { get; set; } = "";
    }
}
