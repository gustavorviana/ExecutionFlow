using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Hangfire
{
    public interface IHangfireDispatcher : IEventDispatcher, IRecurringTrigger
    {
    }
}
