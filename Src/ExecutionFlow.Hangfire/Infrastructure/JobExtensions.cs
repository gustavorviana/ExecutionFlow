using Hangfire.Common;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal static class JobExtensions
    {
        public static bool IsRecurring(this Job job)
        {
            if (job?.Method == null)
                return false;

            return job.Method.DeclaringType == typeof(HangfireJobDispatcher) &&
                job.Method.Name == nameof(HangfireJobDispatcher.DispatchRecurringAsync);
        }
    }
}
