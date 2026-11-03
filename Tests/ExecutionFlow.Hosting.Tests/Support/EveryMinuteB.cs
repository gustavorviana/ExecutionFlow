using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:01:00")]
    internal sealed class EveryMinuteB : ProbeHandler
    {
        public EveryMinuteB(RunProbe probe) : base(probe) { }
    }
}
