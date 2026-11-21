using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:01:00")]
    [NoOverlap(90)]
    internal sealed class NoOverlapMinute : ProbeHandler
    {
        public NoOverlapMinute(RunProbe probe) : base(probe) { }
    }
}
