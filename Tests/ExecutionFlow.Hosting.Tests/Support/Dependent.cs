using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>No schedule: the plan triggers it.</summary>
    internal sealed class Dependent : ProbeHandler
    {
        public Dependent(RunProbe probe) : base(probe) { }
    }
}
