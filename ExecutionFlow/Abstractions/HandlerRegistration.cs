using System;

namespace ExecutionFlow.Abstractions
{
    public class HandlerRegistration
    {
        public Type HandlerType { get; }
        public Type JobType { get; }
        public Type ServiceType { get; }
        public bool IsRecurring { get; }
        public string DisplayName { get; }
        public string Cron { get; }

        public HandlerRegistration(Type handlerType, Type jobType, Type serviceType, bool isRecurring, string displayName, string cron)
        {
            HandlerType = handlerType;
            JobType = jobType;
            ServiceType = serviceType;
            IsRecurring = isRecurring;
            DisplayName = displayName;
            Cron = cron;
        }
    }
}
