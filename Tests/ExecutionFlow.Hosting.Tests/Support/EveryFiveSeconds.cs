using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:00:05")]
    internal sealed class EveryFiveSeconds : ProbeHandler
    {
        public EveryFiveSeconds(RunProbe probe) : base(probe) { }
    }
}
