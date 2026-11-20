using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [Recurring("*/30 * * * * *")]
    internal sealed class EveryThirtySecondsCron : ProbeHandler
    {
        public EveryThirtySecondsCron(RunProbe probe) : base(probe) { }
    }
}
