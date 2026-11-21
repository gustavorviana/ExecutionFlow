using System;

namespace ExecutionFlow.Abstractions.Events
{
    /// <summary>
    /// Describes an exception thrown by a lifecycle hook. Hook exceptions never affect the job or the other hooks.
    /// </summary>
    public class HookErrorContext
    {
        /// <summary>Gets the hook type that threw.</summary>
        public Type HookType { get; }

        /// <summary>Gets the hook interface that was being invoked (e.g. <see cref="IOnFailed"/>).</summary>
        public Type HookInterface { get; }

        /// <summary>Gets the event passed to the hook.</summary>
        public ExecutionEvent Event { get; }

        /// <summary>Gets the exception thrown by the hook.</summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HookErrorContext"/>.
        /// </summary>
        public HookErrorContext(Type hookType, Type hookInterface, ExecutionEvent @event, Exception exception)
        {
            HookType = hookType;
            HookInterface = hookInterface;
            Event = @event;
            Exception = exception;
        }
    }
}
