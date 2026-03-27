using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExecutionFlow
{
    /// <summary>
    /// Provides context when a <see cref="ReflectionTypeLoadException"/> occurs during assembly scanning.
    /// </summary>
    public class AssemblyTypeScanContext
    {
        /// <summary>Gets the assembly that was being scanned.</summary>
        public Assembly Assembly { get; }

        /// <summary>Gets the exception that occurred during type loading.</summary>
        public ReflectionTypeLoadException Exception { get; }

        /// <summary>Gets the types that were successfully loaded before the exception.</summary>
        public IReadOnlyList<Type> LoadedTypes { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="AssemblyTypeScanContext"/>.
        /// </summary>
        /// <param name="assembly">The assembly being scanned.</param>
        /// <param name="exception">The type load exception.</param>
        /// <param name="loadedTypes">The types that loaded successfully.</param>
        public AssemblyTypeScanContext(Assembly assembly, ReflectionTypeLoadException exception, IReadOnlyList<Type> loadedTypes)
        {
            Assembly = assembly;
            Exception = exception;
            LoadedTypes = loadedTypes;
        }
    }
}
