namespace ExecutionFlow.Abstractions
{
    public interface IExecutionLoggerFactory
    {
        IExecutionLogger CreateLogger(FlowParameters parameters);
    }
}
