using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>10:00 in São Paulo (UTC-3) is 13:00 UTC.</summary>
    [Recurring("0 10 * * *", TimeZone = "America/Sao_Paulo")]
    internal sealed class TenAmSaoPaulo : ProbeHandler
    {
        public TenAmSaoPaulo(RunProbe probe) : base(probe) { }
    }
}
