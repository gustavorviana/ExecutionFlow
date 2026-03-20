using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public abstract class ExecutionFlowSetup<TOptions> : IExecutionFlowRegistry where TOptions : ExecutionFlowOptions, new()
    {
        public IReadOnlyDictionary<Type, EventJobRegistryInfo> EventHandlers { get; private set; } = new Dictionary<Type, EventJobRegistryInfo>();

        public IReadOnlyDictionary<Type, RecurringJobRegistryInfo> RecurringHandlers { get; private set; } = new Dictionary<Type, RecurringJobRegistryInfo>();

        public IReadOnlyList<Type> LoggerFactoryTypes => Options?.LoggerFactoryTypes;

        public TOptions Options { get; } = new TOptions();

        public void Configure(Action<TOptions> configure)
        {
            configure(Options);
            Options.Lock();

            EventHandlers = Options.EventHandlers;
            RecurringHandlers = Options.RecurringHandlers;

            OnConfigured(Options);
        }

        protected abstract void OnConfigured(TOptions options);
    }
}