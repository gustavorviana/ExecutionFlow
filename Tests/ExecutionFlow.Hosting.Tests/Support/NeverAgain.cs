using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>February 30th never comes.</summary>
    [Recurring("0 0 30 2 *")]
    internal sealed class NeverAgain : ProbeHandler
    {
        public NeverAgain(RunProbe probe) : base(probe) { }
    }
}
