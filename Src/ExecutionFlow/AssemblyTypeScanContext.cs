using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExecutionFlow
{
    public class AssemblyTypeScanContext
    {
        public Assembly Assembly { get; }
        public ReflectionTypeLoadException Exception { get; }
        public IReadOnlyList<Type> LoadedTypes { get; }

        public AssemblyTypeScanContext(Assembly assembly, ReflectionTypeLoadException exception, IReadOnlyList<Type> loadedTypes)
        {
            Assembly = assembly;
            Exception = exception;
            LoadedTypes = loadedTypes;
        }
    }
}
