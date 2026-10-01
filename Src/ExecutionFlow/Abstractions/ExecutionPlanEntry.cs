using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// The configuration of one recurring handler in an <see cref="ExecutionPlanner"/>: its prerequisites, whether it's
    /// enabled, and its display name.
    /// </summary>
    public sealed class ExecutionPlanEntry
    {
        private readonly ExecutionPlanner _owner;
        private readonly List<Type> _prerequisites = new List<Type>();
        private bool _enabled = true;
        private string _displayName;

        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the declared prerequisites, in declaration order.</summary>
        public IReadOnlyList<Type> Prerequisites => _prerequisites;

        /// <summary>Gets whether the handler is enabled. Default: <c>true</c>.</summary>
        public bool IsEnabled => _enabled;

        /// <summary>Gets the display name set with <see cref="DisplayName(string)"/>, or <c>null</c>.</summary>
        public string ConfiguredDisplayName => _displayName;

        /// <summary>Gets the minimum time between the end of a run and the next, set with <see cref="MinInterval(TimeSpan)"/>, or <c>null</c>.</summary>
        public TimeSpan? ConfiguredMinInterval { get; private set; }

        /// <summary>Gets whether <see cref="RunOnOwnSchedule"/> was set.</summary>
        public bool RunsOnOwnSchedule { get; private set; }

        /// <summary>
        /// Triggers this dependent as soon as its prerequisites are ready (the default), but never less than
        /// <paramref name="interval"/> after its previous run finished. A trigger that comes too early is postponed, not lost.
        /// </summary>
        /// <param name="interval">The minimum time between the end of a run and the next run.</param>
        /// <returns>This entry, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is zero or negative.</exception>
        public ExecutionPlanEntry MinInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "The minimum interval must be positive.");

            _owner.ThrowIfBuilt();
            ConfiguredMinInterval = interval;
            return this;
        }

        /// <summary>
        /// Runs this dependent on its own schedule instead of being triggered by its prerequisites. At each occurrence it runs
        /// only if every prerequisite completed a new cycle; otherwise the occurrence records that its prerequisites weren't met.
        /// </summary>
        /// <returns>This entry, for chaining.</returns>
        public ExecutionPlanEntry RunOnOwnSchedule()
        {
            _owner.ThrowIfBuilt();
            RunsOnOwnSchedule = true;
            return this;
        }

        internal ExecutionPlanEntry(ExecutionPlanner owner, Type handlerType)
        {
            _owner = owner;
            HandlerType = handlerType;
        }

        /// <summary>
        /// Declares that this handler only runs after a new completed cycle of <typeparamref name="TPrerequisite"/>, before
        /// every run, not only the first.
        /// </summary>
        /// <typeparam name="TPrerequisite">The recurring handler that must complete a cycle first.</typeparam>
        /// <returns>This entry, for chaining.</returns>
        public ExecutionPlanEntry DependsOn<TPrerequisite>() where TPrerequisite : class, IHandler
        {
            return DependsOn(typeof(TPrerequisite));
        }

        /// <summary>
        /// Declares that this handler only runs after a new completed cycle of <paramref name="prerequisite"/>, before every
        /// run, not only the first. Declaring the same prerequisite again has no effect.
        /// </summary>
        /// <param name="prerequisite">The recurring handler that must complete a cycle first.</param>
        /// <returns>This entry, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The handler depends on itself, or the planner was already built.</exception>
        public ExecutionPlanEntry DependsOn(Type prerequisite)
        {
            if (prerequisite == null) throw new ArgumentNullException(nameof(prerequisite));
            _owner.ThrowIfBuilt();

            if (ExecutionPlanner.TypeComparer.Equals(prerequisite, HandlerType))
                throw new InvalidOperationException(
                    $"'{HandlerType.FullName}' cannot depend on itself: it would wait for its own cycle forever.");

            if (!_prerequisites.Contains(prerequisite, ExecutionPlanner.TypeComparer))
                _prerequisites.Add(prerequisite);

            return this;
        }

        /// <summary>
        /// Enables or disables the handler. A disabled handler stays in the plan, so it's reported, but providers don't run it.
        /// </summary>
        /// <param name="enabled">Whether the handler runs. Default: <c>true</c>.</param>
        /// <returns>This entry, for chaining.</returns>
        public ExecutionPlanEntry Enabled(bool enabled = true)
        {
            _owner.ThrowIfBuilt();
            _enabled = enabled;
            return this;
        }

        /// <summary>
        /// Sets the handler's display name, which takes precedence over <see cref="System.ComponentModel.DisplayNameAttribute"/>
        /// and the class name.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>This entry, for chaining.</returns>
        public ExecutionPlanEntry DisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("The display name cannot be empty.", nameof(displayName));

            _owner.ThrowIfBuilt();
            _displayName = displayName;
            return this;
        }
    }
}
