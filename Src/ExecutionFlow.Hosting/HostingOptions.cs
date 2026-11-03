using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hosting
{
    /// <summary>
    /// Configures the local host (<see cref="ServiceCollectionExtensions.AddExecutionFlowHosting"/>), which runs recurring
    /// handlers inside the .NET Generic Host without Hangfire.
    /// </summary>
    /// <remarks>
    /// The local host runs <b>locally and isolated</b>: it coordinates nothing outside itself. Two apps or processes using it
    /// never communicate (unlike Hangfire servers sharing a storage), and two registrations in the same process don't either:
    /// each has its own plan cycles, overlap guards, concurrency limit and schedules, so a handler registered in both runs in
    /// both. Per-handler configuration goes through <see cref="ExecutionFlowOptions.GetPlanOf{THandler}"/> or the execution
    /// plan; these options only hold the global defaults.
    /// </remarks>
    public class HostingOptions : ExecutionFlowOptions
    {
        /// <summary>Gets or sets whether recurring handlers run on their schedule when nothing more specific sets it. Default is <c>true</c>.</summary>
        public bool GlobalRecurringAutoRun { get => _globalRecurringAutoRun; set { ThrowIfLocked(); _globalRecurringAutoRun = value; } }
        private bool _globalRecurringAutoRun = true;

        /// <summary>
        /// Gets or sets the default time zone ID for cron schedules (e.g. "America/Sao_Paulo"). <c>null</c> (default) means UTC.
        /// Overridden by <see cref="ExecutionFlow.Attributes.RecurringAttribute.TimeZone"/> and by the handler's
        /// <c>TimeZone(...)</c> in the plan or <see cref="ExecutionFlowOptions.GetPlanOf{THandler}"/>. Intervals have no time zone.
        /// </summary>
        public string RecurringTimeZone { get => _recurringTimeZone; set { ThrowIfLocked(); _recurringTimeZone = value; } }
        private string _recurringTimeZone;

        /// <summary>Gets or sets how many handler runs execute at the same time. Default is <see cref="Environment.ProcessorCount"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        public int MaxConcurrency
        {
            get => _maxConcurrency;
            set
            {
                ThrowIfLocked();
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "MaxConcurrency must be at least 1.");
                _maxConcurrency = value;
            }
        }
        private int _maxConcurrency = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets whether the host runs every enabled handler once, in plan order, and then stops the application instead
        /// of scheduling them. The process exit code is 1 when any handler failed. Default is <c>false</c>.
        /// </summary>
        public bool RunOnce { get => _runOnce; set { ThrowIfLocked(); _runOnce = value; } }
        private bool _runOnce;

        /// <summary>
        /// Gets or sets whether an interval counts from the end of the previous run instead of on a fixed cadence from the host
        /// start. Default is <c>false</c>. A handler overrides it with <see cref="HandlerBuilderExtensions.IntervalFromEnd{THandler}"/>.
        /// </summary>
        /// <remarks>Intervals count from the host start, unlike Hangfire's clock-aligned exact crons.</remarks>
        public bool IntervalFromEnd { get => _intervalFromEnd; set { ThrowIfLocked(); _intervalFromEnd = value; } }
        private bool _intervalFromEnd;

        /// <summary>
        /// Gets or sets whether, for a handler whose runs may not overlap, an occurrence that arrives during a run is skipped
        /// instead of waiting for it (up to the overlap timeout). Default is <c>false</c>. A handler overrides it with
        /// <see cref="HandlerBuilderExtensions.SkipWhenOverlapping{THandler}"/>.
        /// </summary>
        public bool SkipWhenOverlapping { get => _skipWhenOverlapping; set { ThrowIfLocked(); _skipWhenOverlapping = value; } }
        private bool _skipWhenOverlapping;

#if NET8_0_OR_GREATER
        /// <summary>Gets or sets the time source for every clock read and delay. Default is <see cref="TimeProvider.System"/>.</summary>
        public TimeProvider TimeProvider
        {
            get => _timeProvider;
            set { ThrowIfLocked(); _timeProvider = value ?? throw new ArgumentNullException(nameof(value)); }
        }
        private TimeProvider _timeProvider = TimeProvider.System;
#endif

        /// <summary>
        /// The local host runs every handler of the plan: <see cref="ExecutionFlowOptions.UsePlan"/> registers them as recurring
        /// handlers. A handler in the plan ignores its <see cref="ExecutionFlowOptions.GetPlanOf{THandler}"/> values, with a
        /// trace warning.
        /// </summary>
        /// <param name="plan">The plan built with <see cref="ExecutionPlanner"/>.</param>
        protected override void OnPlanRegistering(ExecutionPlan plan)
        {
            foreach (var step in plan.Steps)
                Add(step.HandlerType);
        }

        /// <summary><see cref="GlobalRecurringAutoRun"/>.</summary>
        protected override bool IsRecurringHandlerEnabledByDefault()
        {
            return GlobalRecurringAutoRun;
        }

        /// <summary><see cref="RecurringTimeZone"/>.</summary>
        protected override string DefaultRecurringTimeZone => RecurringTimeZone;

        internal bool IsEnabled(Type handlerType) => ResolveRecurringHandlerEnabled(handlerType);

        internal string TimeZoneIdOf(RecurringJobRegistryInfo registration) => ResolveRecurringTimeZoneId(registration);

        internal string ConfiguredTimeZoneOf(Type handlerType) => GetConfiguredTimeZone(handlerType);

        internal IReadOnlyDictionary<string, object> SettingsOf(Type handlerType) => ResolveRecurringHandlerSettings(handlerType);

        /// <summary>The handler's <see cref="HandlerBuilderExtensions.IntervalFromEnd{THandler}"/>, else <see cref="IntervalFromEnd"/>.</summary>
        internal bool IntervalFromEndOf(Type handlerType)
        {
            return SettingsOf(handlerType).TryGetValue(HandlerBuilderExtensions.IntervalFromEndKey, out var value) ? (bool)value : IntervalFromEnd;
        }

        /// <summary>The handler's <see cref="HandlerBuilderExtensions.SkipWhenOverlapping{THandler}"/>, else <see cref="SkipWhenOverlapping"/>.</summary>
        internal bool SkipWhenOverlappingOf(Type handlerType)
        {
            return SettingsOf(handlerType).TryGetValue(HandlerBuilderExtensions.SkipWhenOverlappingKey, out var value) ? (bool)value : SkipWhenOverlapping;
        }
    }
}
