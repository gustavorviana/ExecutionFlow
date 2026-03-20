namespace ExecutionFlow.Abstractions
{
    public interface IExecutionLogger
    {
        void Log(HandlerLogType level, string message, params object[] args);
    }
}
