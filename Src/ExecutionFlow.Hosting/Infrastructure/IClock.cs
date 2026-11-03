using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>The time source of the local host: every clock read and delay goes through it, so tests can control time.</summary>
    internal interface IClock
    {
        /// <summary>Gets the current UTC time.</summary>
        DateTimeOffset UtcNow { get; }

        /// <summary>Completes after <paramref name="delay"/>; a negative delay completes at once.</summary>
        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }
}
