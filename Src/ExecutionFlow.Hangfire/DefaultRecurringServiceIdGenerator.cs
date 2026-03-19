using System;

namespace ExecutionFlow.Hangfire
{
    public class DefaultRecurringServiceIdGenerator : IJobIdGenerator
    {
        public string GenerateId(Type type)
        {
            return type.FullName;
        }
    }
}