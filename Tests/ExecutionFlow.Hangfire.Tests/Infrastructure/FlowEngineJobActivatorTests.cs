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
}
