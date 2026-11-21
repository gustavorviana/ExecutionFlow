using ExecutionFlow.Abstractions;
using System;

namespace ExecutionFlow.Attributes
{
    /// <summary>
    /// Sets how a dependent recurring handler (one with <see cref="DependsOnAttribute"/>) runs, other than the default of
    /// being triggered as soon as every prerequisite completed a new cycle. Put it on the dependent handler.
    /// </summary>
    /// <remarks>
    /// <see cref="HandlerBuilder.RunMode(DependentRunMode, TimeSpan?)"/> in the execution plan replaces it as a whole.
    /// </remarks>
    /// <example>
    /// <code>
    /// [DependsOn(typeof(CategorySyncHandler))]
    /// [DependentRun(DependentRunMode.MinInterval, "00:20:00")]
    /// public class ProductSyncHandler : IHandler { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DependentRunAttribute : Attribute
    {
        /// <summary>Gets the run mode.</summary>
        public DependentRunMode Mode { get; }

        /// <summary>
        /// Gets the minimum interval as written, in the invariant format <c>[d.]hh:mm:ss</c> (any positive value): required for
        /// <see cref="DependentRunMode.MinInterval"/>, not allowed for <see cref="DependentRunMode.OwnSchedule"/>.
        /// </summary>
        public string Interval { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="DependentRunAttribute"/>.
        /// </summary>
        /// <param name="mode">The run mode.</param>
        /// <param name="interval">The minimum interval, <c>[d.]hh:mm:ss</c>, for <see cref="DependentRunMode.MinInterval"/>.</param>
        public DependentRunAttribute(DependentRunMode mode, string interval = null)
        {
            Mode = mode;
            Interval = interval;
        }

        /// <summary>Resolves the attribute of <paramref name="handlerType"/> into a mode and interval, validating them.</summary>
        /// <exception cref="InvalidOperationException">The mode and interval disagree, or the interval is invalid or not positive.</exception>
        internal TimeSpan? ResolveInterval(Type handlerType)
        {
            if (Mode == DependentRunMode.OwnSchedule)
            {
                if (Interval != null)
                    throw new InvalidOperationException(
                        $"[DependentRun] on '{handlerType.FullName}' sets DependentRunMode.OwnSchedule with the interval '{Interval}'; OwnSchedule doesn't take one.");
                return null;
            }

            if (Interval == null)
                throw new InvalidOperationException(
                    $"[DependentRun] on '{handlerType.FullName}' sets DependentRunMode.MinInterval without an interval. Pass it as \"hh:mm:ss\".");

            if (!TimeSpanText.TryParse(Interval, out var interval) || interval <= TimeSpan.Zero)
                throw new InvalidOperationException(
                    $"[DependentRun] on '{handlerType.FullName}' has the invalid minimum interval '{Interval}'. Use a positive [d.]hh:mm:ss, e.g. \"00:20:00\".");

            return interval;
        }
    }
}
