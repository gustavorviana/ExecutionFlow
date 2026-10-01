using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// One recurring handler in a built <see cref="ExecutionPlan"/>, with everything a provider needs to run or report it.
    /// </summary>
    public sealed class ExecutionPlanStep
    {
        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Gets the display name: <see cref="ExecutionPlanEntry.DisplayName(string)"/>, else
        /// <see cref="System.ComponentModel.DisplayNameAttribute"/>, else the class name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets whether the handler runs: it's enabled and, under <see cref="MissingPrerequisite.SkipDependents"/>, every
        /// prerequisite is available.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>Gets why the handler doesn't run, or <c>null</c> when <see cref="Enabled"/> is <c>true</c>.</summary>
        public string SkipReason { get; }

        /// <summary>Gets the handlers that must complete a new cycle before each run of this one.</summary>
        public IReadOnlyList<Type> Prerequisites { get; }

        /// <summary>Gets the handlers that wait for this one.</summary>
        public IReadOnlyList<Type> Dependents { get; }

        /// <summary>
        /// Gets the minimum time between the end of a run and the next, or <c>null</c>. Only for dependents triggered by their
        /// prerequisites.
        /// </summary>
        public TimeSpan? MinInterval { get; }

        /// <summary>
        /// Gets whether this dependent runs on its own schedule, gated by its prerequisites, instead of being triggered by them.
        /// </summary>
        public bool RunsOnOwnSchedule { get; }

        internal ExecutionPlanStep(Type handlerType, string displayName, string skipReason, IReadOnlyList<Type> prerequisites, IReadOnlyList<Type> dependents,
            TimeSpan? minInterval, bool runsOnOwnSchedule)
        {
            MinInterval = minInterval;
            RunsOnOwnSchedule = runsOnOwnSchedule;
            HandlerType = handlerType;
            DisplayName = displayName;
            Enabled = skipReason == null;
            SkipReason = skipReason;
            Prerequisites = prerequisites;
            Dependents = dependents;
        }
    }
}
