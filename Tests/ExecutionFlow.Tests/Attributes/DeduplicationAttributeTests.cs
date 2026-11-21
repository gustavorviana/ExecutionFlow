using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Tests.Attributes;

public class DeduplicationAttributeTests
{
    [Fact]
    public void Behavior_ReturnsValuePassedToConstructor()
    {
        var attribute = new DeduplicationAttribute(DeduplicationBehavior.ReplaceExisting);

        Assert.Equal(DeduplicationBehavior.ReplaceExisting, attribute.Behavior);
    }

    [Fact]
    public void Attribute_IsInheritedByDerivedEvents()
    {
        var attribute = (DeduplicationAttribute?)Attribute.GetCustomAttribute(typeof(DerivedEvent), typeof(DeduplicationAttribute), inherit: true);

        Assert.NotNull(attribute);
        Assert.Equal(DeduplicationBehavior.SkipIfExists, attribute!.Behavior);
    }

    [Fact]
    public void ExecutionFlowOptions_DeduplicationBehavior_DefaultsToDisabled()
    {
        Assert.Equal(DeduplicationBehavior.Disabled, new TestOptions().DeduplicationBehavior);
    }

    [Deduplication(DeduplicationBehavior.SkipIfExists)]
    public class BaseEvent { }

    public class DerivedEvent : BaseEvent { }

    public class TestOptions : ExecutionFlowOptions { }
}
