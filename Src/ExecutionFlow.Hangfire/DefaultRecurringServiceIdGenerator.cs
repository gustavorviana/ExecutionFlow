using System;

namespace ExecutionFlow.Hangfire
{
    public class DefaultRecurringServiceIdGenerator : IJobIdGenerator
    {
        public string GenerateId(Type handlerType)
        {
            return handlerType.FullName;
        }
    }
}