using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using NSubstitute;

namespace ExecutionFlow.Hangfire.Tests.Infrastructure;

public class FlowEngineJobActivatorTests
{
    private static FlowEngineJobActivator Create(IExecutionFlowRegistry? registry = null)
        => new FlowEngineJobActivator(registry ?? Substitute.For<IExecutionFlowRegistry>());

    [Fact]
    public void DefaultRegistrations_JobActivator_Returns_Self()
    {
        var activator = Create();

        var result = activator.GetService(typeof(JobActivator));

        Assert.Same(activator, result);
    }

    [Fact]
    public void DefaultRegistrations_IServiceProvider_Returns_Self()
    {
        var activator = Create();

        var result = activator.GetService(typeof(IServiceProvider));

        Assert.Same(activator, result);
    }

    [Fact]
    public void DefaultRegistrations_Registry_Returns_Registry()
    {
        var registry = Substitute.For<IExecutionFlowRegistry>();
        var activator = new FlowEngineJobActivator(registry);

        var result = activator.GetService(typeof(IExecutionFlowRegistry));

        Assert.Same(registry, result);
    }

    [Fact]
    public void GetService_UnregisteredConcreteType_CreatesInstance()
    {
        var activator = Create();

        var result = activator.GetService(typeof(SimpleService));

        Assert.NotNull(result);
        Assert.IsType<SimpleService>(result);
    }

    [Fact]
    public void GetService_UnregisteredConcreteType_CreatesNewInstanceEachTime()
    {
        var activator = Create();

        var result1 = activator.GetService(typeof(SimpleService));
        var result2 = activator.GetService(typeof(SimpleService));

        Assert.NotSame(result1, result2);
    }

    [Fact]
    public void AddSingleton_WithInstance_ReturnsExactInstance()
    {
        var activator = Create();
        var instance = new SimpleService();
        activator.AddSingleton<SimpleService>(instance);

        var result = activator.GetService(typeof(SimpleService));

        Assert.Same(instance, result);
    }

    [Fact]
    public void AddSingleton_WithType_CreatesSingleton()
    {
        var activator = Create();
        activator.AddSingleton<IMyService>(typeof(MyService));

        var result1 = activator.GetService(typeof(IMyService));
        var result2 = activator.GetService(typeof(IMyService));

        Assert.NotNull(result1);
        Assert.Same(result1, result2);
    }

    [Fact]
    public void AddSingleton_WithFunc_LazyEvaluatesOnce()
    {
        var activator = Create();
        var callCount = 0;
        activator.AddSingleton(() => { callCount++; return new SimpleService(); });

        _ = activator.GetService(typeof(SimpleService));
        _ = activator.GetService(typeof(SimpleService));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GetService_Interface_WithoutRegistration_Throws()
    {
        var activator = Create();

        Assert.Throws<InvalidOperationException>(() =>
            activator.GetService(typeof(IUnregisteredInterface)));
    }

    [Fact]
    public void GetService_Abstract_WithoutRegistration_Throws()
    {
        var activator = Create();

        Assert.Throws<InvalidOperationException>(() =>
            activator.GetService(typeof(AbstractService)));
    }

    [Fact]
    public void ActivateJob_DelegatesToGetService()
    {
        var activator = Create();
        var instance = new SimpleService();
        activator.AddSingleton<SimpleService>(instance);

        var result = activator.ActivateJob(typeof(SimpleService));

        Assert.Same(instance, result);
    }

    [Fact]
    public void Constructor_Injection_ResolvesParameters()
    {
        var activator = Create();
        var dep = new SimpleService();
        activator.AddSingleton<SimpleService>(dep);

        var result = activator.GetService(typeof(ServiceWithDependency));

        var service = Assert.IsType<ServiceWithDependency>(result);
        Assert.Same(dep, service.Dependency);
    }

    [Fact]
    public void RegisterLoggerFactory_CreatesExecutionLoggerFactory()
    {
        var activator = Create();

        activator.RegisterLoggerFactory(new[] { typeof(TestLoggerFactory) });

        var factory = activator.GetService(typeof(ExecutionLoggerFactory));
        Assert.NotNull(factory);
        Assert.IsType<ExecutionLoggerFactory>(factory);
    }

    // AC-004.2
    [Fact]
    public void GetService_ConstructorCycle_ThrowsNamingTheChain()
    {
        var activator = Create();

        var ex = Assert.Throws<InvalidOperationException>(() => activator.GetService(typeof(CycleA)));

        Assert.Contains(nameof(CycleA), ex.Message);
        Assert.Contains(nameof(CycleB), ex.Message);

        // A cycle through a registered type is caught too.
        activator.AddSingleton<ICycleService>(typeof(CycleThroughRegistration));
        Assert.Throws<InvalidOperationException>(() => activator.GetService(typeof(ICycleService)));
    }

    // AC-004.3
    [Fact]
    public void AddSingleton_WithType_CreatesOnce_WhenResolvedConcurrently()
    {
        var activator = Create();
        activator.AddSingleton<SlowService>();
        SlowService.Reset();

        var results = ResolveConcurrently(activator, typeof(SlowService));

        Assert.Equal(1, SlowService.Created);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    // AC-004.3
    [Fact]
    public void AddSingleton_WithFunc_RunsOnce_WhenResolvedConcurrently()
    {
        var activator = Create();
        var calls = 0;
        activator.AddSingleton(() =>
        {
            Interlocked.Increment(ref calls);
            Thread.Sleep(50);
            return new SimpleService();
        });

        var results = ResolveConcurrently(activator, typeof(SimpleService));

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    private static object[] ResolveConcurrently(FlowEngineJobActivator activator, Type type)
    {
        const int threads = 16;
        using var start = new Barrier(threads);
        var tasks = Enumerable.Range(0, threads)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                return activator.GetService(type)!;
            }, TaskCreationOptions.LongRunning))
            .ToArray();

        Task.WaitAll(tasks);
        return tasks.Select(t => t.Result).ToArray();
    }

    // --- Test types ---

    public class SimpleService { }

    public interface IMyService { }
    public class MyService : IMyService { }

    public interface IUnregisteredInterface { }

    public abstract class AbstractService { }

    public class ServiceWithDependency
    {
        public SimpleService Dependency { get; }
        public ServiceWithDependency(SimpleService dependency) { Dependency = dependency; }
    }

    public class TestLoggerFactory : IExecutionLoggerFactory
    {
        public IExecutionLogger CreateLogger(FlowParameters parameters) => null!;
    }

    public class CycleA { public CycleA(CycleB b) { } }
    public class CycleB { public CycleB(CycleA a) { } }

    public interface ICycleService { }
    public class CycleThroughRegistration : ICycleService { public CycleThroughRegistration(ICycleService self) { } }

    public class SlowService
    {
        private static int _created;

        public static int Created => _created;

        public static void Reset() => _created = 0;

        public SlowService()
        {
            Interlocked.Increment(ref _created);
            Thread.Sleep(50);
        }
    }
}
