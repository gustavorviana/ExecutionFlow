using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Common;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal class HandlerJobFilterProvider : IJobFilterProvider
    {
        private readonly IExecutionFlowRegistry _registry;
        private readonly HangfireOptions _options;

        public HandlerJobFilterProvider(IExecutionFlowRegistry registry, HangfireOptions options)
        {
            _registry = registry;
            _options = options;
        }

        public IEnumerable<JobFilter> GetFilters(Job job)
        {
            if (job == null)
                return System.Array.Empty<JobFilter>();

            var jobInfo = HangfireJobInfo.Create(job);
            var handlerType = jobInfo?.GetHandlerType(_registry);
            if (handlerType == null)
                return System.Array.Empty<JobFilter>();

            var filters = handlerType
                .GetCustomAttributes(true)
                .OfType<JobFilterAttribute>()
                .Select((attr, i) => new JobFilter(attr, JobFilterScope.Type, i))
                .ToList();

            if (_options.DisableRecurringRetries
                && job.IsRecurring()
                && !filters.Any(f => f.Instance is AutomaticRetryAttribute))
            {
                filters.Add(new JobFilter(new AutomaticRetryAttribute { Attempts = 0 }, JobFilterScope.Type, filters.Count));
            }

            return filters;
        }
    }
}
