using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Default implementation of <see cref="IJobIdGenerator"/> that uses the handler type's full name as the job ID.
    /// </summary>
    public class DefaultRecurringServiceIdGenerator : IJobIdGenerator
    {
        /// <inheritdoc />
        public string GenerateId(Type handlerType)
        {
            return handlerType.FullName;
        }
    }
}
