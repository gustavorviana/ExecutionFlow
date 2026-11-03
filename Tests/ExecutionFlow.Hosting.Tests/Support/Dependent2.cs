using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    internal sealed class Dependent2 : ProbeHandler
    {
        public Dependent2(RunProbe probe) : base(probe) { }
    }
}
