using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [Recurring("0 * * * *")]
    internal sealed class Hourly : ProbeHandler
    {
        public Hourly(RunProbe probe) : base(probe) { }
    }
}
