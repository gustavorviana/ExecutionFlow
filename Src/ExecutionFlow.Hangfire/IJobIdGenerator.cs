using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Generates a deterministic identifier for a recurring service/job.
    /// </summary>
    public interface IJobIdGenerator
    {
        string GenerateId(Type type);
    }
}
