using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    public abstract class ExecutionFlowSetup<TOptions> : IExecutionFlowRegistry where TOptions : ExecutionFlowOptions, new()
    {
        public IReadOnlyDictionary<Type, HandlerRegistration> EventHandlers { get; private set; } = new Dictionary<Type, HandlerRegistration>();

        public IReadOnlyList<HandlerRegistration> RecurringHandlers { get; private set; } = new List<HandlerRegistration>();

        protected TOptions Options { get; } = new TOptions();

        public void Configure(Action<TOptions> configure)
        {
            configure(Options);
            Options.Lock();

            EventHandlers = Options
                .HandlerTypes
                .Where(x => !x.IsRecurring)
                .ToDictionary(x => x.EventType, x => x,new TypeEqualityComparer());

            RecurringHandlers = Options
                .HandlerTypes
                .Where(x => x.IsRecurring)
                .ToArray();

            OnConfigured(Options);
        }

        protected abstract void OnConfigured(TOptions options);
    }
}