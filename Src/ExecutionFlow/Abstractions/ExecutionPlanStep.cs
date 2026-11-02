using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// One recurring handler in a built <see cref="ExecutionPlan"/>, with everything a provider needs to run or report it.
    /// </summary>
    /// <remarks>
    /// The <c>Configured…</c> members and <see cref="Cron"/>, <see cref="Interval"/>, <see cref="Id"/>, <see cref="TimeZone"/>
    /// are the builder's explicit values (<c>null</c> when not set): providers resolve each of those items independently,
    /// builder first, then the per-handler option, then the attribute, then the global default.
    /// </remarks>
    public sealed class ExecutionPlanStep
    {
        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Gets the display name: <see cref="HandlerBuilder.DisplayName(string)"/>, else
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

        /// <summary>
        /// Gets how long a run waits for a running one before it fails, or <c>null</c> when runs may overlap:
        /// <see cref="HandlerBuilder.NoOverlap(TimeSpan)"/> / <see cref="HandlerBuilder.AllowOverlap"/>, else
        /// <see cref="Attributes.NoOverlapAttribute"/>.
        /// </summary>
        public TimeSpan? OverlapTimeout { get; }

        /// <summary>Gets the cron set with <see cref="HandlerBuilder.Cron(string)"/>, or <c>null</c>.</summary>
        public string Cron { get; }

        /// <summary>Gets the schedule interval set with <see cref="HandlerBuilder.Interval(TimeSpan)"/>, or <c>null</c>.</summary>
        public TimeSpan? Interval { get; }

        /// <summary>Gets the recurring job ID set with <see cref="HandlerBuilder.Id(string)"/>, or <c>null</c>.</summary>
        public string Id { get; }

        /// <summary>Gets the time zone ID set with <see cref="HandlerBuilder.TimeZone(string)"/>, or <c>null</c>.</summary>
        public string TimeZone { get; }

        /// <summary>Gets the value set with <see cref="HandlerBuilder.Enabled(bool)"/>, or <c>null</c> when the provider's option decides.</summary>
        public bool? ConfiguredEnabled { get; }

        /// <summary>Gets the provider settings set with <see cref="HandlerBuilder.HasSetting(string, object)"/>.</summary>
        public IReadOnlyDictionary<string, object> Settings { get; }

        internal ExecutionPlanStep(Type handlerType, string displayName, string skipReason, IReadOnlyList<Type> prerequisites, IReadOnlyList<Type> dependents,
            TimeSpan? minInterval, bool runsOnOwnSchedule, TimeSpan? overlapTimeout,
            string cron, TimeSpan? interval, string id, string timeZone, bool? configuredEnabled, IReadOnlyDictionary<string, object> settings)
        {
            HandlerType = handlerType;
            DisplayName = displayName;
            Enabled = skipReason == null;
            SkipReason = skipReason;
            Prerequisites = prerequisites;
            Dependents = dependents;
            MinInterval = minInterval;
            RunsOnOwnSchedule = runsOnOwnSchedule;
            OverlapTimeout = overlapTimeout;
            Cron = cron;
            Interval = interval;
            Id = id;
            TimeZone = timeZone;
            ConfiguredEnabled = configuredEnabled;
            Settings = settings;
        }

        /// <summary>A copy with another skip reason, for re-evaluating availability with the provider's on/off.</summary>
        internal ExecutionPlanStep WithSkipReason(string skipReason)
        {
            return new ExecutionPlanStep(HandlerType, DisplayName, skipReason, Prerequisites, Dependents, MinInterval, RunsOnOwnSchedule,
                OverlapTimeout, Cron, Interval, Id, TimeZone, ConfiguredEnabled, Settings);
        }
    }
}
