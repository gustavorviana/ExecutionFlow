using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Configures one recurring handler in an <see cref="ExecutionPlanner"/>, inline (<see cref="ExecutionPlanner.Add(Type)"/>)
    /// or from an <see cref="IHandlerConfiguration{THandler}"/>. Each call sets an explicit value for one item; items that
    /// aren't called keep their attribute, option or default, as EF Core's fluent API does over data annotations.
    /// </summary>
    /// <remarks>
    /// Precedence per item, lowest to highest: global default &lt; attribute &lt; per-handler option &lt; this builder.
    /// Dependencies are the exception: the class's <see cref="Attributes.DependsOnAttribute"/> and <see cref="DependsOn(Type)"/>
    /// merge, and only <see cref="RemoveDependency(Type)"/> or <see cref="ClearDependencies"/> drop one.
    /// </remarks>
    public class HandlerBuilder
    {
        private readonly ExecutionPlanner _owner;
        private readonly List<Type> _prerequisites = new List<Type>();
        private readonly Dictionary<string, object> _settings = new Dictionary<string, object>(StringComparer.Ordinal);

        internal HandlerBuilder(ExecutionPlanner owner, Type handlerType)
        {
            _owner = owner;
            HandlerType = handlerType;
        }

        /// <summary>Gets the handler type.</summary>
        public Type HandlerType { get; }

        /// <summary>Gets the prerequisites: the class's <see cref="Attributes.DependsOnAttribute"/> plus the builder's, in declaration order.</summary>
        public IReadOnlyList<Type> Prerequisites => _prerequisites;

        /// <summary>Gets the value set with <see cref="Enabled(bool)"/>, or <c>null</c> when the option or the default decides.</summary>
        public bool? ConfiguredEnabled { get; private set; }

        /// <summary>Gets whether the planner treats the handler as enabled: <see cref="ConfiguredEnabled"/>, else <c>true</c>.</summary>
        public bool IsEnabled => ConfiguredEnabled ?? true;

        /// <summary>Gets the display name set with <see cref="DisplayName(string)"/>, or <c>null</c>.</summary>
        public string ConfiguredDisplayName { get; private set; }

        /// <summary>Gets the cron set with <see cref="Cron(string)"/>, or <c>null</c>.</summary>
        public string ConfiguredCron { get; private set; }

        /// <summary>Gets the schedule interval set with <see cref="Interval(TimeSpan)"/>, or <c>null</c>.</summary>
        public TimeSpan? ConfiguredInterval { get; private set; }

        /// <summary>Gets the recurring job ID set with <see cref="Id(string)"/>, or <c>null</c>.</summary>
        public string ConfiguredId { get; private set; }

        /// <summary>Gets the time zone ID set with <see cref="TimeZone(string)"/>, or <c>null</c>.</summary>
        public string ConfiguredTimeZone { get; private set; }

        /// <summary>Gets the run mode set with <see cref="RunMode(DependentRunMode, TimeSpan?)"/>, or <c>null</c>.</summary>
        public DependentRunMode? ConfiguredRunMode { get; private set; }

        /// <summary>Gets the minimum interval of <see cref="DependentRunMode.MinInterval"/>, or <c>null</c>.</summary>
        public TimeSpan? ConfiguredMinInterval => ConfiguredRunMode == DependentRunMode.MinInterval ? _runModeInterval : null;

        /// <summary>Gets whether the run mode is <see cref="DependentRunMode.OwnSchedule"/>.</summary>
        public bool RunsOnOwnSchedule => ConfiguredRunMode == DependentRunMode.OwnSchedule;

        private TimeSpan? _runModeInterval;

        /// <summary>Gets whether <see cref="NoOverlap(TimeSpan)"/> or <see cref="AllowOverlap"/> was called.</summary>
        public bool IsOverlapConfigured { get; private set; }

        /// <summary>Gets the overlap timeout set with <see cref="NoOverlap(TimeSpan)"/>; <c>null</c> after <see cref="AllowOverlap"/> or when not configured.</summary>
        public TimeSpan? ConfiguredOverlapTimeout { get; private set; }

        /// <summary>Gets the provider settings set with <see cref="HasSetting(string, object)"/>.</summary>
        public IReadOnlyDictionary<string, object> Settings => _settings;

        /// <summary>Schedules the handler with a cron expression, replacing any interval (from the builder or the class).</summary>
        /// <param name="cron">The cron expression.</param>
        public HandlerBuilder Cron(string cron)
        {
            if (string.IsNullOrWhiteSpace(cron)) throw new ArgumentException("The cron expression cannot be empty.", nameof(cron));
            _owner.ThrowIfBuilt();
            ConfiguredCron = cron;
            ConfiguredInterval = null;
            return this;
        }

        /// <summary>Schedules the handler every <paramref name="interval"/>, replacing any cron (from the builder or the class).</summary>
        /// <param name="interval">The interval, at least one minute.</param>
        public HandlerBuilder Interval(TimeSpan interval)
        {
            if (interval < TimeSpan.FromMinutes(1))
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be at least one minute.");

            _owner.ThrowIfBuilt();
            ConfiguredInterval = interval;
            ConfiguredCron = null;
            return this;
        }

        /// <summary>Sets an explicit, stable recurring job ID.</summary>
        /// <param name="id">The ID.</param>
        public HandlerBuilder Id(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("The recurring job ID cannot be empty.", nameof(id));
            _owner.ThrowIfBuilt();
            ConfiguredId = id;
            return this;
        }

        /// <summary>
        /// Sets the time zone the cron is evaluated in. An unknown ID fails when the provider is configured; on an interval
        /// it has no effect and the provider warns.
        /// </summary>
        /// <param name="timeZoneId">The time zone ID, e.g. "America/Sao_Paulo".</param>
        public HandlerBuilder TimeZone(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
                throw new ArgumentException($"The time zone of '{HandlerType.FullName}' cannot be null or empty.", nameof(timeZoneId));

            _owner.ThrowIfBuilt();
            ConfiguredTimeZone = timeZoneId;
            return this;
        }

        /// <summary>Sets the display name, which takes precedence over <see cref="System.ComponentModel.DisplayNameAttribute"/> and the class name.</summary>
        /// <param name="displayName">The display name.</param>
        public HandlerBuilder DisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("The display name cannot be empty.", nameof(displayName));

            _owner.ThrowIfBuilt();
            ConfiguredDisplayName = displayName;
            return this;
        }

        /// <summary>
        /// Enables or disables the handler. A disabled handler stays in the plan, so it's reported, but providers don't run it.
        /// It takes precedence over the provider's per-handler option, which then logs a warning.
        /// </summary>
        /// <param name="enabled">Whether the handler runs. Default: <c>true</c>.</param>
        public HandlerBuilder Enabled(bool enabled = true)
        {
            _owner.ThrowIfBuilt();
            ConfiguredEnabled = enabled;
            return this;
        }

        /// <summary>
        /// Declares that this handler only runs after a new completed cycle of <paramref name="prerequisite"/>, before every
        /// run, not only the first. Declaring the same prerequisite again has no effect.
        /// </summary>
        /// <param name="prerequisite">The recurring handler that must complete a cycle first.</param>
        /// <exception cref="InvalidOperationException">The handler depends on itself, or the planner was already built.</exception>
        public HandlerBuilder DependsOn(Type prerequisite)
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

        /// <summary>Removes a prerequisite, including one from <see cref="Attributes.DependsOnAttribute"/>. Removing one that isn't declared has no effect.</summary>
        /// <param name="prerequisite">The prerequisite to remove.</param>
        public HandlerBuilder RemoveDependency(Type prerequisite)
        {
            if (prerequisite == null) throw new ArgumentNullException(nameof(prerequisite));
            _owner.ThrowIfBuilt();
            _prerequisites.RemoveAll(p => ExecutionPlanner.TypeComparer.Equals(p, prerequisite));
            return this;
        }

        /// <summary>Removes every prerequisite, including those from <see cref="Attributes.DependsOnAttribute"/>.</summary>
        public HandlerBuilder ClearDependencies()
        {
            _owner.ThrowIfBuilt();
            _prerequisites.Clear();
            return this;
        }

        /// <summary>
        /// Sets how this dependent runs, replacing any <c>[DependentRun]</c> on the class. Only for handlers with prerequisites.
        /// </summary>
        /// <param name="mode">The run mode.</param>
        /// <param name="interval">The minimum interval: required and positive for <see cref="DependentRunMode.MinInterval"/>, not allowed for <see cref="DependentRunMode.OwnSchedule"/>.</param>
        public HandlerBuilder RunMode(DependentRunMode mode, TimeSpan? interval = null)
        {
            if (mode == DependentRunMode.MinInterval)
            {
                if (!interval.HasValue)
                    throw new ArgumentException($"'{HandlerType.FullName}': DependentRunMode.MinInterval needs an interval.", nameof(interval));
                if (interval.Value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(interval), interval, "The minimum interval must be positive.");
            }
            else if (interval.HasValue)
            {
                throw new ArgumentException($"'{HandlerType.FullName}': DependentRunMode.{mode} doesn't take an interval.", nameof(interval));
            }

            _owner.ThrowIfBuilt();
            ConfiguredRunMode = mode;
            _runModeInterval = interval;
            return this;
        }

        /// <summary>
        /// Prevents runs of this handler from overlapping: a run that starts while another one is in progress waits for it,
        /// and fails if <paramref name="timeout"/> passes first. Takes precedence over <see cref="Attributes.NoOverlapAttribute"/>.
        /// </summary>
        /// <param name="timeout">How long a run waits for the running one before it fails.</param>
        public HandlerBuilder NoOverlap(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The overlap timeout must be positive.");

            _owner.ThrowIfBuilt();
            IsOverlapConfigured = true;
            ConfiguredOverlapTimeout = timeout;
            return this;
        }

        /// <summary>Allows overlapping runs again, cancelling a <see cref="Attributes.NoOverlapAttribute"/> on the class.</summary>
        public HandlerBuilder AllowOverlap()
        {
            _owner.ThrowIfBuilt();
            IsOverlapConfigured = true;
            ConfiguredOverlapTimeout = null;
            return this;
        }

        /// <summary>
        /// Stores a provider-specific setting (for provider packages' extension methods). Use keys prefixed with the provider
        /// name, e.g. "Hangfire:Queue"; setting a key again replaces its value.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        public HandlerBuilder HasSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("The setting key cannot be empty.", nameof(key));
            _owner.ThrowIfBuilt();
            _settings[key] = value;
            return this;
        }
    }

    /// <summary>
    /// Configures <typeparamref name="THandler"/> in an <see cref="ExecutionPlanner"/>; every method returns this builder for chaining.
    /// </summary>
    /// <typeparam name="THandler">The recurring handler.</typeparam>
    public sealed class HandlerBuilder<THandler> : HandlerBuilder where THandler : class, IHandler
    {
        internal HandlerBuilder(ExecutionPlanner owner)
            : base(owner, typeof(THandler))
        {
        }

        /// <inheritdoc cref="HandlerBuilder.Cron(string)"/>
        public new HandlerBuilder<THandler> Cron(string cron) { base.Cron(cron); return this; }

        /// <inheritdoc cref="HandlerBuilder.Interval(TimeSpan)"/>
        public new HandlerBuilder<THandler> Interval(TimeSpan interval) { base.Interval(interval); return this; }

        /// <inheritdoc cref="HandlerBuilder.Id(string)"/>
        public new HandlerBuilder<THandler> Id(string id) { base.Id(id); return this; }

        /// <inheritdoc cref="HandlerBuilder.TimeZone(string)"/>
        public new HandlerBuilder<THandler> TimeZone(string timeZoneId) { base.TimeZone(timeZoneId); return this; }

        /// <inheritdoc cref="HandlerBuilder.DisplayName(string)"/>
        public new HandlerBuilder<THandler> DisplayName(string displayName) { base.DisplayName(displayName); return this; }

        /// <inheritdoc cref="HandlerBuilder.Enabled(bool)"/>
        public new HandlerBuilder<THandler> Enabled(bool enabled = true) { base.Enabled(enabled); return this; }

        /// <inheritdoc cref="HandlerBuilder.DependsOn(Type)"/>
        /// <typeparam name="TPrerequisite">The recurring handler that must complete a cycle first.</typeparam>
        public HandlerBuilder<THandler> DependsOn<TPrerequisite>() where TPrerequisite : class, IHandler { base.DependsOn(typeof(TPrerequisite)); return this; }

        /// <inheritdoc cref="HandlerBuilder.DependsOn(Type)"/>
        public new HandlerBuilder<THandler> DependsOn(Type prerequisite) { base.DependsOn(prerequisite); return this; }

        /// <inheritdoc cref="HandlerBuilder.RemoveDependency(Type)"/>
        /// <typeparam name="TPrerequisite">The prerequisite to remove.</typeparam>
        public HandlerBuilder<THandler> RemoveDependency<TPrerequisite>() where TPrerequisite : class, IHandler { base.RemoveDependency(typeof(TPrerequisite)); return this; }

        /// <inheritdoc cref="HandlerBuilder.RemoveDependency(Type)"/>
        public new HandlerBuilder<THandler> RemoveDependency(Type prerequisite) { base.RemoveDependency(prerequisite); return this; }

        /// <inheritdoc cref="HandlerBuilder.ClearDependencies"/>
        public new HandlerBuilder<THandler> ClearDependencies() { base.ClearDependencies(); return this; }

        /// <inheritdoc cref="HandlerBuilder.RunMode(DependentRunMode, TimeSpan?)"/>
        public new HandlerBuilder<THandler> RunMode(DependentRunMode mode, TimeSpan? interval = null) { base.RunMode(mode, interval); return this; }

        /// <inheritdoc cref="HandlerBuilder.NoOverlap(TimeSpan)"/>
        public new HandlerBuilder<THandler> NoOverlap(TimeSpan timeout) { base.NoOverlap(timeout); return this; }

        /// <inheritdoc cref="HandlerBuilder.AllowOverlap"/>
        public new HandlerBuilder<THandler> AllowOverlap() { base.AllowOverlap(); return this; }

        /// <inheritdoc cref="HandlerBuilder.HasSetting(string, object)"/>
        public new HandlerBuilder<THandler> HasSetting(string key, object value) { base.HasSetting(key, value); return this; }
    }
}
