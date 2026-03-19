using Hangfire.Common;

namespace ExecutionFlow.Hangfire
{
    public interface IHangfireJobName
    {
        string GetName(Job job);
    }
}
