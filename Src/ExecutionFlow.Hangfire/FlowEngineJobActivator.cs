using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Dispatcher;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire
{
    internal class FlowEngineJobActivator : JobActivator
    {
        private readonly IExecutionFlowRegistry _registry;

        public FlowEngineJobActivator(IExecutionFlowRegistry registry)
        {
            _registry = registry;
        }

        public override object ActivateJob(Type jobType)
        {
            if (jobType == typeof(HangfireJobDispatcher))
                return new HangfireJobDispatcher(this, _registry);

            return base.ActivateJob(jobType);
        }
    }
}