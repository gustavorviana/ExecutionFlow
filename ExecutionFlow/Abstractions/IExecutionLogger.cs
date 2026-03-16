using System;

namespace ExecutionFlow.Abstractions
{
    public interface IExecutionLogger
    {
        void Info(object message);
        void Success(object message);
        void Warning(object message);
        void Error(object message);
        void Error(Exception exception);
    }
}
