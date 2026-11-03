using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    internal sealed class Unscheduled : ProbeHandler
    {
        public Unscheduled(RunProbe probe) : base(probe) { }
    }
}
