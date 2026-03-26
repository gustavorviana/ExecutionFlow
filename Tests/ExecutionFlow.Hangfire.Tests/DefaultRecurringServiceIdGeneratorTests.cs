namespace ExecutionFlow.Hangfire.Tests;

public class DefaultRecurringServiceIdGeneratorTests
{
    [Fact]
    public void GenerateId_ReturnsFullName()
    {
        var generator = new DefaultRecurringServiceIdGenerator();

        var id = generator.GenerateId(typeof(DefaultRecurringServiceIdGeneratorTests));

        Assert.Equal(typeof(DefaultRecurringServiceIdGeneratorTests).FullName, id);
    }

    [Fact]
    public void GenerateId_ReturnsSameId_ForSameType()
    {
        var generator = new DefaultRecurringServiceIdGenerator();

        var id1 = generator.GenerateId(typeof(string));
        var id2 = generator.GenerateId(typeof(string));

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateId_ReturnsDifferentIds_ForDifferentTypes()
    {
        var generator = new DefaultRecurringServiceIdGenerator();

        var id1 = generator.GenerateId(typeof(string));
        var id2 = generator.GenerateId(typeof(int));

        Assert.NotEqual(id1, id2);
    }
}
