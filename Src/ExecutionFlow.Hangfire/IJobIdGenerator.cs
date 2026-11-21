using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Generates deterministic identifiers for recurring jobs.
    /// </summary>
    public interface IJobIdGenerator
    {
        /// <summary>
        /// Generates a unique identifier for a recurring handler type.
        /// </summary>
        /// <param name="type">The handler type.</param>
        /// <returns>
        /// A deterministic job ID string. It must contain only letters, digits, spaces and <c>_ - . + ` [ ] , =</c>, that doesn't start or end with a space; otherwise resolving the recurring job throws <see cref="ArgumentException"/> naming
        /// the handler. A type's full name always qualifies.
        /// </returns>
        string GenerateId(Type type);
    }
}
