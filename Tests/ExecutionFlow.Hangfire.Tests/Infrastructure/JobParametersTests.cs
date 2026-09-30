using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class JobParametersTests
{
    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();

    [Fact]
    public void ReadCustomId_ReadsLegacyRawValue()
    {
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns("order-42");

        Assert.Equal("order-42", JobParameters.ReadCustomId(_connection, "job-1"));
    }

    [Fact]
    public void ReadCustomId_ReadsJsonEncodedValue()
    {
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns("\"order-42\"");

        Assert.Equal("order-42", JobParameters.ReadCustomId(_connection, "job-1"));
    }

    [Fact]
    public void ReadCustomId_ReturnsNull_WhenParameterMissing()
    {
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns((string?)null);

        Assert.Null(JobParameters.ReadCustomId(_connection, "job-1"));
    }

    [Fact]
    public void WriteCustomId_WritesJsonEncodedValue_ThatReadsBack()
    {
        string? stored = null;
        _connection.When(c => c.SetJobParameter("job-1", ContextConsts.CustomId, Arg.Any<string>()))
            .Do(ci => stored = ci.ArgAt<string>(2));
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns(_ => stored);

        JobParameters.WriteCustomId(_connection, "job-1", "order-42");

        Assert.Equal("\"order-42\"", stored);
        Assert.Equal("order-42", JobParameters.ReadCustomId(_connection, "job-1"));
    }

    [Fact]
    public void WriteCustomId_Null_ReadsBackAsNull()
    {
        string? stored = null;
        _connection.When(c => c.SetJobParameter("job-1", ContextConsts.CustomId, Arg.Any<string>()))
            .Do(ci => stored = ci.ArgAt<string>(2));
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns(_ => stored);

        JobParameters.WriteCustomId(_connection, "job-1", null!);

        Assert.Null(JobParameters.ReadCustomId(_connection, "job-1"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("order-42", true)]
    public void TryGetCustomId_ReturnsTrue_OnlyForNonEmptyCustomId(string? customId, bool expected)
    {
        Assert.Equal(expected, JobParameters.TryGetCustomId(new CustomIdEvent(customId), out _));
    }

    [Fact]
    public void TryGetCustomId_ReturnsFalse_WhenEventHasNoCustomId()
    {
        Assert.False(JobParameters.TryGetCustomId(new object(), out _));
    }

    public class CustomIdEvent(string? customId) : ExecutionFlow.Abstractions.ICustomIdEvent
    {
        public string CustomId => customId!;
    }
}
