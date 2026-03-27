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
        /// <returns>A deterministic job ID string.</returns>
        string GenerateId(Type type);
    }
}
