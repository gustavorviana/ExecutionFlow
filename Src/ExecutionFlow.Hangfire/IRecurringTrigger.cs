using System;

namespace ExecutionFlow.Hangfire
{
    public interface IRecurringTrigger
    {
        void Trigger(Type handlerType);
        void Trigger(string jobId);
    }
}
