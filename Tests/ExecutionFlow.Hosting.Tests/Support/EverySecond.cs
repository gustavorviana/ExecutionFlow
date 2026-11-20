using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:00:01")]
    internal sealed class EverySecond : ProbeHandler
    {
        public EverySecond(RunProbe probe) : base(probe) { }
    }
}
