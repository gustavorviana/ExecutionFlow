using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class DeduplicationStoreTests
{
    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();

    private void SetupKey(string customId, string jobId, string? stateName)
    {
        _connection.GetAllEntriesFromHash(DeduplicationStore.GetKey(customId))
            .Returns(new Dictionary<string, string> { [DeduplicationStore.JobIdField] = jobId });
        _connection.GetStateData(jobId).Returns(stateName == null ? null : new StateData { Name = stateName });
    }

    [Theory]
    [InlineData("Enqueued")]
    [InlineData("Scheduled")]
    [InlineData("Awaiting")]
    [InlineData("Processing")]
    public void FindActiveJobId_ReturnsJobId_WhenReservedJobIsActive(string stateName)
    {
        SetupKey("order-1", "job-1", stateName);

        Assert.Equal("job-1", DeduplicationStore.FindActiveJobId(_connection, "order-1"));
    }

    [Theory]
    [InlineData("Succeeded")]
    [InlineData("Deleted")]
    [InlineData("Failed")]
    [InlineData(null)]
    public void FindActiveJobId_ReturnsNull_WhenKeyIsStale(string? stateName)
    {
        SetupKey("order-1", "job-1", stateName);

        Assert.Null(DeduplicationStore.FindActiveJobId(_connection, "order-1"));
    }

    [Fact]
    public void FindActiveJobId_ReturnsNull_WhenNoKey()
    {
        _connection.GetAllEntriesFromHash(Arg.Any<string>()).Returns((Dictionary<string, string>?)null);

        Assert.Null(DeduplicationStore.FindActiveJobId(_connection, "order-1"));
    }

    [Fact]
    public void Reserve_WritesKeyAndSetsExpiration_WhenStorageSupportsIt()
    {
        var transaction = Substitute.For<JobStorageTransaction>();
        _connection.CreateWriteTransaction().Returns(transaction);

        DeduplicationStore.Reserve(_connection, "order-1", "job-1", TimeSpan.FromDays(30));

        transaction.Received(1).SetRangeInHash(DeduplicationStore.GetKey("order-1"),
            Arg.Is<IEnumerable<KeyValuePair<string, string>>>(e => e.Any(kv => kv.Key == DeduplicationStore.JobIdField && kv.Value == "job-1")));
        transaction.Received(1).ExpireHash(DeduplicationStore.GetKey("order-1"), TimeSpan.FromDays(30));
        transaction.Received(1).Commit();
    }

    [Fact]
    public void Release_RemovesKey_OnlyWhenItPointsToTheJob()
    {
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        SetupKey("order-1", "job-2", "Enqueued");

        DeduplicationStore.Release(_connection, transaction, "order-1", "job-1");
        transaction.DidNotReceiveWithAnyArgs().RemoveHash(default!);

        DeduplicationStore.Release(_connection, transaction, "order-1", "job-2");
        transaction.Received(1).RemoveHash(DeduplicationStore.GetKey("order-1"));
    }
}
