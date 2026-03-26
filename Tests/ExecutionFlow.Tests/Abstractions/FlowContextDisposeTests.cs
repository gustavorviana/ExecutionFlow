using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests.Abstractions;

public class FlowContextDisposeTests
{
    [Fact]
    public void Dispose_PreventsOnCustomIdChangeCallback()
    {
        var callbackInvoked = false;
        var logger = Substitute.For<IExecutionLogger>();
        var parameters = new FlowParameters();

        var context = new FlowContext<string>(parameters, logger, "event", _ => callbackInvoked = true);

        ((IDisposable)context).Dispose();
        context.SetCustomId("after-dispose");

        Assert.False(callbackInvoked);
        Assert.Equal("after-dispose", context.CustomId);
    }

    [Fact]
    public void UsingStatement_DisposesContext()
    {
        var callCount = 0;
        var logger = Substitute.For<IExecutionLogger>();
        var parameters = new FlowParameters();

        FlowContext<string> ctx;
        using (ctx = new FlowContext<string>(parameters, logger, "event", _ => callCount++))
        {
            ctx.SetCustomId("inside");
            Assert.Equal(1, callCount);
        }

        ctx.SetCustomId("outside");
        Assert.Equal(1, callCount);
    }
}
