using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;

namespace ExecutionFlow.Hosting.Tests.Support
{
    internal static class Wait
    {
        /// <summary>Waits (real time) until <paramref name="condition"/> holds; fails after 5 seconds.</summary>
        public static async Task Until(Func<bool> condition, string because)
        {
            for (var i = 0; i < 500; i++)
            {
                if (condition())
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Timed out waiting until " + because);
        }
    }
}
