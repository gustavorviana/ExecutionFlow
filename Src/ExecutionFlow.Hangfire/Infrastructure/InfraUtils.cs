using Hangfire.Storage.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal static class InfraUtils
    {
        private const int PageSize = 10;

        public static IEnumerable<TDto> ReadAll<TDto>(string queueName, Func<string, int, int, JobList<TDto>> function)
        {
            var from = 0;
            while (true)
            {
                var jobs = function(queueName, from, PageSize);
                if (jobs == null || jobs.Count == 0)
                    break;

                foreach (var job in jobs.Select(x => x.Value))
                    yield return job;

                if (jobs.Count < PageSize)
                    break;

                from += PageSize;
            }
        }

        public static IEnumerable<TDto> ReadAll<TDto>(Func<int, int, JobList<TDto>> function)
        {
            var from = 0;
            while (true)
            {
                var jobs = function(from, PageSize);
                if (jobs == null || jobs.Count == 0)
                    break;

                foreach (var job in jobs.Select(x => x.Value))
                    yield return job;

                if (jobs.Count < PageSize)
                    break;

                from += PageSize;
            }
        }
    }
}