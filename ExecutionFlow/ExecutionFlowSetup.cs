using System;
using System.Collections.Generic;
using ExecutionFlow.Abstractions;

namespace ExecutionFlow
{
    public static class ExecutionFlowSetup
    {
        private static IReadOnlyList<HandlerRegistration> _registrations;

        public static IReadOnlyList<HandlerRegistration> Registrations =>
            _registrations ?? throw new InvalidOperationException("ExecutionFlowSetup.Configure has not been called.");

        public static void Configure(Action<ExecutionFlowOptions> configure)
        {
            var options = new ExecutionFlowOptions();
            configure(options);
            options.Lock();
            _registrations = options.Registrations;
        }
    }
}
