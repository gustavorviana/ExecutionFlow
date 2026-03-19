using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionManager
    {
        bool IsRunning(string customId);
        bool IsPending(string customId);
        void Cancel(string customId);
        IEnumerable<JobInfo> GetJobs(JobState state);
    }
}
