using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [Recurring("* * * * *")]
    internal sealed class EveryMinuteCron : ProbeHandler
    {
        public EveryMinuteCron(RunProbe probe) : base(probe) { }
    }
}
