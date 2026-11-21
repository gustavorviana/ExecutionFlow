namespace ExecutionFlow.Hosting.Abstractions
{
    /// <summary>The outcome of one handler in <see cref="IExecutionPlanRunner.RunOnceAsync"/>.</summary>
    public enum HandlerRunStatus
    {
        /// <summary>The handler ran and completed.</summary>
        Completed,

        /// <summary>The handler ran and threw.</summary>
        Failed,

        /// <summary>The handler didn't run: it's disabled, or a prerequisite didn't complete in the same run.</summary>
        Skipped,
    }
}
