using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    [RecurringInterval("00:01:00")]
    internal sealed class DependentOwn : ProbeHandler
    {
        public DependentOwn(RunProbe probe) : base(probe) { }
    }
}
