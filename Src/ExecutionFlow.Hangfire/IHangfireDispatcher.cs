using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Combined dispatcher interface for publishing events and triggering recurring jobs.
    /// </summary>
    public interface IHangfireDispatcher : IEventDispatcher, IRecurringTrigger
    {
    }
}
