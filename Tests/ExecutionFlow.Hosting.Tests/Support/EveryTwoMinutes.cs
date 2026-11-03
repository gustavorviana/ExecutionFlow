using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:02:00")]
    internal sealed class EveryTwoMinutes : ProbeHandler
    {
        public EveryTwoMinutes(RunProbe probe) : base(probe) { }
    }
}
