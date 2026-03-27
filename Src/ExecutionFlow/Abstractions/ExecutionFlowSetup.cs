using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Base class for configuring and building ExecutionFlow handler registrations.
    /// </summary>
    /// <typeparam name="TOptions">The options type used for configuration.</typeparam>
    public abstract class ExecutionFlowSetup<TOptions> : IExecutionFlowRegistry where TOptions : ExecutionFlowOptions, new()
    {
        /// <inheritdoc />
        public IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers { get; private set; } = new Dictionary<Type, EventJobRegistryInfo>();

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers { get; private set; } = new Dictionary<Type, RecurringJobRegistryInfo>();

        /// <summary>Gets the registered logger factory types.</summary>
        public IReadOnlyList<Type> LoggerFactoryTypes => Options?.LoggerFactoryTypes;

        /// <summary>Gets the configuration options.</summary>
        public TOptions Options { get; } = new TOptions();

        /// <summary>
        /// Configures the setup with the provided options callback, then locks the options.
        /// </summary>
        /// <param name="configure">The configuration callback.</param>
        public void Configure(Action<TOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(Options);
            Options.Lock();

            EventHandlers = Options.EventHandlers;
            RecurringHandlers = Options.RecurringHandlers;

            OnConfigured(Options);
        }

        /// <summary>
        /// Called after configuration is complete. Override to perform additional validation or setup.
        /// </summary>
        /// <param name="options">The locked configuration options.</param>
        protected abstract void OnConfigured(TOptions options);
    }
}
