using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Provides metadata about a registered handler.
    /// </summary>
    public interface IJobRegistryInfo
    {
        /// <summary>
        /// Gets the handler type.
        /// </summary>
        Type HandlerType { get; }

        /// <summary>
        /// Gets the display name for the handler, used in dashboards and logging.
        /// </summary>
        string DisplayName { get; }
    }
}
