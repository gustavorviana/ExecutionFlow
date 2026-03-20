using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionLoggerFactory
    {
        IExecutionLogger CreateLogger(IDictionary<string, object> parameters);
    }
}
