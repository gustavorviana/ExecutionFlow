using System;

namespace ExecutionFlow.Abstractions
{
    public class RecurringJobRegistryInfo : IJobRegistryInfo
    {
        public Type HandlerType { get; }
        public string DisplayName { get; }
        public string Cron { get; }

        public RecurringJobRegistryInfo(Type handlerType, string displayName, string cron)
        {
            HandlerType = handlerType;
            DisplayName = displayName;
            Cron = cron;
        }
    }
}
