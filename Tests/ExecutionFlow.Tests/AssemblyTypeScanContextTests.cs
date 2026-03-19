using System.Reflection;

namespace ExecutionFlow.Tests;

public class AssemblyTypeScanContextTests
{
    [Fact]
    public void Constructor_Stores_Assembly()
    {
        var assembly = typeof(AssemblyTypeScanContextTests).Assembly;

        var context = new AssemblyTypeScanContext(assembly, null, Array.Empty<Type>());

        Assert.Same(assembly, context.Assembly);
    }

    [Fact]
    public void Constructor_Stores_Exception()
    {
        var exception = new ReflectionTypeLoadException(new[] { typeof(string) }, new[] { new Exception("load error") });

        var context = new AssemblyTypeScanContext(typeof(AssemblyTypeScanContextTests).Assembly, exception, Array.Empty<Type>());

        Assert.Same(exception, context.Exception);
    }

    [Fact]
    public void Constructor_Stores_LoadedTypes()
    {
        var types = new[] { typeof(string), typeof(int) };

        var context = new AssemblyTypeScanContext(typeof(AssemblyTypeScanContextTests).Assembly, null, types);

        Assert.Equal(2, context.LoadedTypes.Count);
        Assert.Contains(typeof(string), context.LoadedTypes);
        Assert.Contains(typeof(int), context.LoadedTypes);
    }

    [Fact]
    public void Exception_IsNull_WhenNotProvided()
    {
        var context = new AssemblyTypeScanContext(typeof(AssemblyTypeScanContextTests).Assembly, null, Array.Empty<Type>());

        Assert.Null(context.Exception);
    }
}
