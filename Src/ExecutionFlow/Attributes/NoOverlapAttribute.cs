using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Prevents runs of a recurring handler from overlapping. A run that starts while another run of the same handler is in
    /// progress waits for it; if <see cref="TimeoutSeconds"/> passes first, the waiting run fails. Without this attribute,
    /// runs may overlap. Every execution provider respects it, so handlers don't depend on a provider's own attribute.
    /// </summary>
    /// <remarks>
    /// <see cref="Abstractions.HandlerBuilder.NoOverlap(TimeSpan)"/> on the execution plan takes precedence over this
    /// attribute. Dependents in the execution plan never overlap, with or without it.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class NoOverlapAttribute : Attribute
    {
        /// <summary>Gets how long, in seconds, a run waits for the running one before it fails. Must be positive.</summary>
        public int TimeoutSeconds { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="NoOverlapAttribute"/>.
        /// </summary>
        /// <param name="timeoutSeconds">How long, in seconds, a run waits for the running one before it fails. Must be positive.</param>
        public NoOverlapAttribute(int timeoutSeconds)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Reads the timeout from <paramref name="handlerType"/>'s attribute, or <c>null</c> when it has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">The attribute's timeout isn't positive.</exception>
        internal static TimeSpan? GetTimeout(Type handlerType)
        {
            var attribute = (NoOverlapAttribute)GetCustomAttribute(handlerType, typeof(NoOverlapAttribute), false);
            if (attribute == null)
                return null;

            if (attribute.TimeoutSeconds <= 0)
                throw new InvalidOperationException(
                    $"[NoOverlap] on '{handlerType.FullName}' has the timeout {attribute.TimeoutSeconds}s; it must be positive.");

            return TimeSpan.FromSeconds(attribute.TimeoutSeconds);
        }
    }
}
