using Hangfire.Common;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal static class JobExtensions
    {
        public static bool IsRecurringOfType(this Job job, Type handlerType)
        {
            if (job == null || !job.IsRecurring())
                return false;

            return job.Args?.Count == HangfireRecurringJobInfo.JobArgSize
                && job.Args[HangfireRecurringJobInfo.EventHandlerIndex] is Type jobHandlerType
                && jobHandlerType == handlerType;
        }

        public static bool IsRecurring(this Job job)
        {
            if (job?.Method == null)
                return false;

            return job.Method.DeclaringType == typeof(HangfireJobDispatcher) &&
                job.Method.Name == nameof(HangfireJobDispatcher.DispatchRecurringAsync);
        }
    }
}
