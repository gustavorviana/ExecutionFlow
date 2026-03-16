using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests;

public class ContextTests
{
    [Fact]
    public void ExecutionContext_Exposes_Log_Property()
    {
        var logger = Substitute.For<IExecutionLogger>();
        var context = new Abstractions.ExecutionContext(logger);

        Assert.Same(logger, context.Log);
    }

    [Fact]
    public void ExecutionContext_TEvent_Exposes_Event_Property()
    {
        var logger = Substitute.For<IExecutionLogger>();
        var evt = new TestEvent { Name = "test" };
        var context = new ExecutionContext<TestEvent>(logger, evt);

        Assert.Same(evt, context.Event);
    }

    [Fact]
    public void SetCustomId_Stores_Value_Readable_Via_CustomId()
    {
        var logger = Substitute.For<IExecutionLogger>();
        var context = new ExecutionContext<TestEvent>(logger, new TestEvent());

        context.SetCustomId("my-custom-name");

        Assert.Equal("my-custom-name", context.CustomId);
    }

    [Fact]
    public void SetCustomId_Overwrites_Previous_Value()
    {
        var logger = Substitute.For<IExecutionLogger>();
        var context = new ExecutionContext<TestEvent>(logger, new TestEvent());

        context.SetCustomId("first");
        context.SetCustomId("second");

        Assert.Equal("second", context.CustomId);
    }

    [Fact]
    public void CustomId_Is_Null_By_Default()
    {
        var logger = Substitute.For<IExecutionLogger>();
        var context = new ExecutionContext<TestEvent>(logger, new TestEvent());

        Assert.Null(context.CustomId);
    }

    public class TestEvent
    {
        public string Name { get; set; } = "";
    }
}
