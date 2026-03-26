using ExecutionFlow.Abstractions;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Infrastructure;
using ExecutionFlow.Hangfire.Infrastructure.Filters;
using ExecutionFlow.Hangfire.Tests.Utils;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests;

public class HangfireValidationTests
{
    // --- HangfireOptions Validations ---

    [Fact]
    public void SetJobName_Throws_ForTypeThatDoesNotImplementIHangfireJobName()
    {
        var options = new HangfireOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.SetJobName(typeof(string)));
        Assert.Contains("does not implement IHangfireJobName", ex.Message);
    }

    [Fact]
    public void SetJobName_Accepts_ValidType()
    {
        var options = new HangfireOptions();

        var exception = Record.Exception(() => options.SetJobName<DefaultHangfireJobName>());

        Assert.Null(exception);
    }

    [Fact]
    public void SetJobIdGeneratorType_Throws_ForTypeThatDoesNotImplementIJobIdGenerator()
    {
        var options = new HangfireOptions();

        var ex = Assert.Throws<ArgumentException>(() => options.SetJobIdGeneratorType(typeof(string)));
        Assert.Contains("does not implement IJobIdGenerator", ex.Message);
    }

    [Fact]
    public void SetJobIdGeneratorType_Accepts_ValidType()
    {
        var options = new HangfireOptions();

        var exception = Record.Exception(() => options.SetJobIdGeneratorType<DefaultRecurringServiceIdGenerator>());

        Assert.Null(exception);
    }

    [Fact]
    public void SetJobName_Throws_ForNullType()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.SetJobName(null!));
    }

    [Fact]
    public void SetJobIdGeneratorType_Throws_ForNullType()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.SetJobIdGeneratorType(null!));
    }

    [Fact]
    public void AddOption_Stores_Value()
    {
        var options = new HangfireOptions();

        options.AddOption(new TestConfig { Name = "test" });

        Assert.Single(options.OptionValues);
    }

    [Fact]
    public void AddOption_Throws_ForNullValue()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddOption<TestConfig>(null!));
    }

    [Fact]
    public void AddStateHandler_Throws_ForNullType()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddStateHandler(null!));
    }

    [Fact]
    public void SetJobAutoRun_Throws_ForNullType()
    {
        var options = new HangfireOptions();

        Assert.Throws<ArgumentNullException>(() => options.SetJobAutoRun(null!, true));
    }

    // --- HangfireJobInfo.Create Validations ---

    [Fact]
    public void HangfireJobInfo_Create_ReturnsNull_ForNullJob()
    {
        var result = HangfireJobInfo.Create(null!);

        Assert.Null(result);
    }

    [Fact]
    public void HangfireRecurringJobInfo_GetHandler_ReturnsNull_WhenHandlerTypeIsNull()
    {
        var job = JobBuilder.CreateRecurringJob(null);
        var info = HangfireJobInfo.Create(job);
        var registry = Substitute.For<IExecutionFlowRegistry>();

        var handler = info!.GetHandler(registry);

        Assert.Null(handler);
    }

    [Fact]
    public void HangfireRecurringJobInfo_GetJobType_ReturnsNull_ForNullJob()
    {
        var result = HangfireRecurringJobInfo.GetJobType(null!);

        Assert.Null(result);
    }

    // --- DefaultHangfireJobName Validations ---

    [Fact]
    public void DefaultHangfireJobName_Throws_ForNullIdGenerator()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultHangfireJobName(null!, Substitute.For<IExecutionFlowRegistry>()));
    }

    [Fact]
    public void DefaultHangfireJobName_Throws_ForNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultHangfireJobName(Substitute.For<IJobIdGenerator>(), null!));
    }

    // --- HangfireStateFilter GetAllInstancesOf Null Filtering ---

    [Fact]
    public void StateFilter_DoesNotThrow_WhenServiceProviderReturnsNull()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(TestStateHandler)).Returns(null);

        var registry = Substitute.For<IExecutionFlowRegistry>();
        var stateHandlers = new List<Type> { typeof(TestStateHandler) };

        var filter = new HangfireStateFilter(registry, serviceProvider, stateHandlers);

        var context = CreateElectStateContext(new EnqueuedState());

        var exception = Record.Exception(() => filter.OnStateElection(context));

        Assert.Null(exception);
    }

    [Fact]
    public void StateFilter_CallsHandler_WhenServiceProviderReturnsInstance()
    {
        var onEnqueued = Substitute.For<IOnEnqueued>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(TestStateHandler)).Returns(onEnqueued);

        var registry = Substitute.For<IExecutionFlowRegistry>();
        var stateHandlers = new List<Type> { typeof(TestStateHandler) };

        var filter = new HangfireStateFilter(registry, serviceProvider, stateHandlers);

        var context = CreateElectStateContext(new EnqueuedState());

        filter.OnStateElection(context);

        onEnqueued.Received(1).OnEnqueued(Arg.Any<ExecutionEvent>());
    }

    // --- HangfireDispatcher Validations ---

    [Fact]
    public void HangfireDispatcher_Trigger_Throws_ForNullType()
    {
        var dispatcher = CreateDispatcher();

        Assert.Throws<ArgumentNullException>(() => dispatcher.Trigger((Type)null!));
    }

    [Fact]
    public void HangfireDispatcher_Trigger_Throws_ForUnregisteredType()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        registry.RecurringHandlers.Returns(new Dictionary<Type, RecurringJobRegistryInfo>());

        var dispatcher = CreateDispatcher(registry: registry);

        Assert.Throws<InvalidOperationException>(() => dispatcher.Trigger(typeof(TestRecurringHandler)));
    }

    [Fact]
    public void HangfireDispatcher_TriggerById_Throws_ForNullOrEmptyId()
    {
        var dispatcher = CreateDispatcher();

        Assert.Throws<ArgumentNullException>(() => dispatcher.Trigger((string)null!));
        Assert.Throws<ArgumentNullException>(() => dispatcher.Trigger(""));
    }

    // --- HangfireOption<T> Validation ---

    [Fact]
    public void HangfireOption_AddOption_StoresAndRetrievesValue()
    {
        var options = new HangfireOptions();
        var config = new TestConfig { Name = "hello" };

        options.AddOption(config);

        var stored = options.OptionValues[typeof(IHangfireOption<TestConfig>)] as IHangfireOption<TestConfig>;
        Assert.NotNull(stored);
        Assert.Equal("hello", stored!.Value.Name);
    }

    // --- HangfireAutoRunFilter Null Safety ---

    [Fact]
    public void AutoRunFilter_DoesNotThrow_WhenParametersSnapshotIsEmpty()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var options = new HangfireOptions();
        options.GlobalRecurringAutoRun = false;

        var filter = new HangfireAutoRunFilter(registry, options);

        var job = JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler));
        var context = CreateElectStateContext(new EnqueuedState(), job: job);

        var exception = Record.Exception(() => filter.OnStateElection(context));

        Assert.Null(exception);
    }

    // --- FlowEngineJobActivator Validations ---

    [Fact]
    public void FlowEngineJobActivator_GetService_ReturnsSingleton_ForRegisteredType()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var activator = new FlowEngineJobActivator(registry);
        activator.AddSingleton<IJobIdGenerator>(typeof(DefaultRecurringServiceIdGenerator));

        var result1 = activator.GetService(typeof(IJobIdGenerator));
        var result2 = activator.GetService(typeof(IJobIdGenerator));

        Assert.Same(result1, result2);
        Assert.IsType<DefaultRecurringServiceIdGenerator>(result1);
    }

    [Fact]
    public void FlowEngineJobActivator_Chaining_ReturnsThis()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var activator = new FlowEngineJobActivator(registry);

        IFlowServiceRegistry result = activator
            .AddSingleton<IJobIdGenerator>(typeof(DefaultRecurringServiceIdGenerator))
            .AddSingleton("test instance");

        Assert.Same(activator, result);
    }

    // Helpers

    private static HangfireDispatcher CreateDispatcher(
        IBackgroundJobClient? jobClient = null,
        IExecutionFlowRegistry? registry = null)
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<IStorageConnection>());

        return new HangfireDispatcher(
            jobClient ?? Substitute.For<IBackgroundJobClient>(),
            storage,
            Substitute.For<IJobIdGenerator>(),
            registry ?? Substitute.For<IExecutionFlowRegistry>());
    }

    private static ElectStateContext CreateElectStateContext(IState candidateState, string? currentState = null, Job? job = null)
    {
        var storage = Substitute.For<JobStorage>();
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        storage.GetConnection().Returns(connection);

        var bgJob = new BackgroundJob("test-job-1", job ?? JobBuilder.CreateRecurringJob(typeof(TestRecurringHandler)), DateTime.UtcNow);

        return new ElectStateContext(
            new ApplyStateContext(storage, connection, transaction, bgJob, candidateState, currentState));
    }

    // Test types

    public class TestConfig
    {
        public string Name { get; set; } = "";
    }

    public class TestStateHandler : IOnEnqueued
    {
        public void OnEnqueued(ExecutionEvent e) { }
    }

    public class TestRecurringHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken ct) => Task.CompletedTask;
    }

    public class TestEvent { }
}
