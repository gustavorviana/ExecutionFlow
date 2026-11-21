using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:01:00")]
    internal sealed class EveryMinute : ProbeHandler
    {
        public EveryMinute(RunProbe probe) : base(probe) { }
    }
}
