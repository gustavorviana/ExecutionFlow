using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Provides read-only access to registered event and recurring handlers.
    /// </summary>
    public interface IExecutionFlowRegistry
    {
        /// <summary>
        /// Gets the registered event handlers, keyed by event type.
        /// </summary>
        IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers { get; }

        /// <summary>
        /// Gets the registered recurring handlers, keyed by handler type.
        /// </summary>
        IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers { get; }
    }
}
