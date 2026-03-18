using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public abstract class ExecutionFlowSetup<TOptions> : IExecutionFlowRegistry where TOptions : ExecutionFlowOptions, new()
    {
        private IReadOnlyList<HandlerRegistration> _registrations = new List<HandlerRegistration>();
        public IReadOnlyList<HandlerRegistration> Registrations => _registrations;
        protected TOptions Options { get; } = new TOptions();

        public void Configure(Action<TOptions> configure)
        {
            configure(Options);
            Options.Lock();

            _registrations = Options.Registrations;
            OnConfigured(Options);
        }

        protected abstract void OnConfigured(TOptions options);
    }
}