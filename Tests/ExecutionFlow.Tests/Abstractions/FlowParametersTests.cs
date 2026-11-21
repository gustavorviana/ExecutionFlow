using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Tests.Abstractions;

public class FlowParametersTests
{
    // --- handlers-and-context F-001: read-only keys are protected in any casing ---

    private static FlowParameters WithReadOnly(string key, object value)
    {
        var builder = new FlowContextBuilder(new ExecutionLoggerFactory(Array.Empty<IExecutionLoggerFactory>()));
        builder.AddReadOnly(key, value);
        return builder.Build().Parameters;
    }

    [Theory]
    [InlineData("performcontext")]
    [InlineData("PERFORMCONTEXT")]
    [InlineData("PerformContext")]
    public void ReadOnlyKey_CannotBeModified_InAnyCasing(string key)
    {
        var parameters = WithReadOnly("PerformContext", "infra");

        Assert.Throws<InvalidOperationException>(() => parameters[key] = null!);
        Assert.Equal("infra", parameters["PerformContext"]);
    }

    [Theory]
    [InlineData("customname")]
    [InlineData("CUSTOMNAME")]
    public void ReadOnlyKey_CannotBeRemoved_InAnyCasing(string key)
    {
        var parameters = WithReadOnly("CustomName", "name");

        Assert.Throws<InvalidOperationException>(() => parameters.Remove(key));
        Assert.True(parameters.ContainsKey("CustomName"));
    }

    [Fact]
    public void Add_NewKey_Succeeds()
    {
        var parameters = new FlowParameters();

        parameters["myKey"] = "myValue";

        Assert.Equal("myValue", parameters["myKey"]);
    }

    [Fact]
    public void Add_CanUpdateOwnKey()
    {
        var parameters = new FlowParameters();
        parameters["myKey"] = "v1";

        parameters["myKey"] = "v2";

        Assert.Equal("v2", parameters["myKey"]);
    }

    [Fact]
    public void Add_CanRemoveOwnKey()
    {
        var parameters = new FlowParameters();
        parameters["myKey"] = "v1";

        var removed = parameters.Remove("myKey");

        Assert.True(removed);
        Assert.False(parameters.ContainsKey("myKey"));
    }

    [Fact]
    public void ReadOnlyKey_CanBeRead()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "value");

        Assert.Equal("value", parameters["infra"]);
    }

    [Fact]
    public void ReadOnlyKey_CannotBeModified()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "value");

        var ex = Assert.Throws<InvalidOperationException>(() => parameters["infra"] = "new");
        Assert.Contains("read-only", ex.Message);
        Assert.Contains("infra", ex.Message);
    }

    [Fact]
    public void ReadOnlyKey_CannotBeRemoved()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "value");

        Assert.Throws<InvalidOperationException>(() => parameters.Remove("infra"));
    }

    [Fact]
    public void ReadOnlyKey_CannotBeAddedViaAdd()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "value");

        Assert.Throws<InvalidOperationException>(() => parameters.Add("infra", "new"));
    }

    [Fact]
    public void TryGetValue_ReturnsTrue_ForExistingKey()
    {
        var parameters = new FlowParameters();
        parameters["key"] = "val";

        Assert.True(parameters.TryGetValue("key", out var value));
        Assert.Equal("val", value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_ForMissingKey()
    {
        var parameters = new FlowParameters();

        Assert.False(parameters.TryGetValue("missing", out _));
    }

    [Fact]
    public void ContainsKey_ReturnsTrueForBothReadOnlyAndUserKeys()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "v1");
        parameters["user"] = "v2";

        Assert.True(parameters.ContainsKey("infra"));
        Assert.True(parameters.ContainsKey("user"));
    }

    [Fact]
    public void Count_IncludesBothReadOnlyAndUserKeys()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "v1");
        parameters["user"] = "v2";

        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Clear_RemovesOnlyUserKeys()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "v1");
        parameters["user1"] = "v2";
        parameters["user2"] = "v3";

        parameters.Remove("user1");
        parameters.Remove("user2");

        Assert.Equal(1, parameters.Count);
        Assert.True(parameters.ContainsKey("infra"));
        Assert.False(parameters.ContainsKey("user1"));
        Assert.False(parameters.ContainsKey("user2"));
    }

    [Fact]
    public void Enumeration_IncludesAllKeys()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("infra", "v1");
        parameters["user"] = "v2";

        var keys = parameters.Keys.ToList();

        Assert.Contains("infra", keys);
        Assert.Contains("user", keys);
    }

    [Fact]
    public void MixedUsage_ReadOnlyAndUserKeys_WorkTogether()
    {
        var parameters = new FlowParameters();
        parameters.AddReadOnly("PerformContext", "ctx");
        parameters.AddReadOnly("EventName", "MyEvent");

        parameters["TipoLog"] = "Auditoria";
        parameters["CorrelationId"] = "abc-123";

        Assert.Equal("ctx", parameters["PerformContext"]);
        Assert.Equal("Auditoria", parameters["TipoLog"]);

        parameters["TipoLog"] = "Debug";
        Assert.Equal("Debug", parameters["TipoLog"]);

        Assert.Throws<InvalidOperationException>(() => parameters["PerformContext"] = "hacked");
    }
}
