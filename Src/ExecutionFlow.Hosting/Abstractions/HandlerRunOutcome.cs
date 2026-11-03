using System;

namespace ExecutionFlow.Hosting.Abstractions
{
    /// <summary>What happened to one handler in <see cref="IExecutionPlanRunner.RunOnceAsync"/>.</summary>
    public sealed class HandlerRunOutcome
    {
        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the outcome.</summary>
        public HandlerRunStatus Status { get; }

        /// <summary>Gets why the handler was skipped, or <c>null</c>.</summary>
        public string Reason { get; }

        /// <summary>Gets the exception of a failed handler, or <c>null</c>.</summary>
        public Exception Exception { get; }

        internal HandlerRunOutcome(Type handlerType, HandlerRunStatus status, string reason = null, Exception exception = null)
        {
            HandlerType = handlerType;
            Status = status;
            Reason = reason;
            Exception = exception;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Reason == null ? $"{HandlerType.Name}: {Status}" : $"{HandlerType.Name}: {Status} ({Reason})";
        }
    }
}
