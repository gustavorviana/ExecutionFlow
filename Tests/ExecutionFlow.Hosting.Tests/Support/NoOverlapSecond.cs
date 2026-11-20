using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:00:01")]
    [NoOverlap(10)]
    internal sealed class NoOverlapSecond : ProbeHandler
    {
        public NoOverlapSecond(RunProbe probe) : base(probe) { }
    }
}
