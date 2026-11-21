using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure.Filters;

public class DeduplicationCleanupFilterTests
{
    private readonly JobStorage _storage = Substitute.For<JobStorage>();
    private readonly IStorageConnection _connection = Substitute.For<IStorageConnection>();
    private readonly IWriteOnlyTransaction _transaction = Substitute.For<IWriteOnlyTransaction>();

    public DeduplicationCleanupFilterTests()
    {
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns("\"order-1\"");
        _connection.GetAllEntriesFromHash(DeduplicationStore.GetKey("order-1"))
            .Returns(new Dictionary<string, string> { [DeduplicationStore.JobIdField] = "job-1" });
    }

    private ApplyStateContext CreateContext(IState newState)
    {
        var job = Job.FromExpression(() => global::System.Console.WriteLine("x"));
        var backgroundJob = new BackgroundJob("job-1", job, DateTime.UtcNow);
        return new ApplyStateContext(_storage, _connection, _transaction, backgroundJob, newState, "Processing");
    }

    public static IEnumerable<object[]> ReleaseStates() => new[]
    {
        new object[] { new SucceededState(null, 0, 0) },
        new object[] { new DeletedState() },
        new object[] { new FailedState(new Exception("x")) }
    };

    [Theory]
    [MemberData(nameof(ReleaseStates))]
    public void OnStateApplied_RemovesKey_WhenJobLeavesActiveStates(IState state)
    {
        new DeduplicationCleanupFilter().OnStateApplied(CreateContext(state), _transaction);

        _transaction.Received(1).RemoveHash(DeduplicationStore.GetKey("order-1"));
    }

    [Fact]
    public void OnStateApplied_KeepsKey_WhenJobIsStillActive()
    {
        new DeduplicationCleanupFilter().OnStateApplied(CreateContext(new EnqueuedState()), _transaction);

        _transaction.DidNotReceiveWithAnyArgs().RemoveHash(default!);
    }

    [Fact]
    public void OnStateApplied_KeepsKey_WhenItPointsToAnotherJob()
    {
        _connection.GetAllEntriesFromHash(DeduplicationStore.GetKey("order-1"))
            .Returns(new Dictionary<string, string> { [DeduplicationStore.JobIdField] = "job-2" });

        new DeduplicationCleanupFilter().OnStateApplied(CreateContext(new SucceededState(null, 0, 0)), _transaction);

        _transaction.DidNotReceiveWithAnyArgs().RemoveHash(default!);
    }

    [Fact]
    public void OnStateApplied_DoesNotThrow_WhenStorageFails()
    {
        _connection.GetJobParameter("job-1", ContextConsts.CustomId).Returns(_ => throw new InvalidOperationException("storage down"));

        new DeduplicationCleanupFilter().OnStateApplied(CreateContext(new SucceededState(null, 0, 0)), _transaction);

        _transaction.DidNotReceiveWithAnyArgs().RemoveHash(default!);
    }
}
