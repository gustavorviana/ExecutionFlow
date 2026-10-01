using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Declares that a recurring handler only runs after a new completed cycle of another recurring handler.
    /// Repeat the attribute for several prerequisites. Read by <see cref="Abstractions.ExecutionPlanner.Add(Type)"/> and
    /// merged with <see cref="Abstractions.ExecutionPlanEntry.DependsOn(Type)"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class DependsOnAttribute : Attribute
    {
        /// <summary>Gets the prerequisite handler type.</summary>
        public Type Prerequisite { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="DependsOnAttribute"/>.
        /// </summary>
        /// <param name="prerequisite">The recurring handler that must complete a cycle first.</param>
        public DependsOnAttribute(Type prerequisite)
        {
            Prerequisite = prerequisite ?? throw new ArgumentNullException(nameof(prerequisite));
        }
    }
}
