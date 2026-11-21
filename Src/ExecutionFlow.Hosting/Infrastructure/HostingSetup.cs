using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>The validated configuration of one local host: registrations, plan and the resolved per-handler values.</summary>
    internal sealed class HostingSetup : ExecutionFlowSetup<HostingOptions>
    {
        /// <summary>Gets the recurring handler types in registration order.</summary>
        public IReadOnlyList<Type> RecurringOrder => Options.RecurringHandlerOrder;

        /// <summary>Gets the execution plan (user or automatic), or <c>null</c>.</summary>
        public ExecutionPlan Plan => Options.Plan;

        protected override void OnConfigured(HostingOptions options)
        {
            ValidateTimeZone(options.RecurringTimeZone, "RecurringTimeZone");

            foreach (var registration in RecurringHandlers.Values)
            {
                // A dependent triggered by its prerequisites needs no schedule and ignores one it has (RunOnOwnSchedule keeps it).
                var hasSchedule = !string.IsNullOrWhiteSpace(registration.Cron) || registration.Interval.HasValue;
                if (IsTriggeredByPlan(registration.HandlerType))
                {
                    if (hasSchedule)
                        Trace.TraceWarning(
                            "ExecutionFlow: '{0}' depends on other handlers in the execution plan, so its prerequisites trigger it and its schedule '{1}' is ignored.",
                            registration.HandlerType.FullName, registration.Cron ?? registration.Interval.ToString());
                }
                else if (!hasSchedule)
                    throw new InvalidOperationException(
                        $"Recurring handler '{registration.HandlerType.FullName}' has no schedule. Add [Recurring(\"<cron>\")] or [RecurringInterval(\"hh:mm:ss\")] to the class, or set Cron/Interval in the plan.");

                // An interval has no time zone: a time zone set for this handler has no effect (the global one is ignored quietly).
                var ignoredTimeZone = registration.Interval.HasValue ? options.ConfiguredTimeZoneOf(registration.HandlerType) : null;
                if (ignoredTimeZone != null)
                {
                    ValidateTimeZone(ignoredTimeZone, registration.HandlerType.FullName);
                    Trace.TraceWarning(
                        "ExecutionFlow: '{0}' runs on an interval, which has no time zone, so its time zone '{1}' is ignored.",
                        registration.HandlerType.FullName, ignoredTimeZone);
                }

                ValidateTimeZone(options.TimeZoneIdOf(registration), registration.HandlerType.FullName);
            }
        }

        private static void ValidateTimeZone(string timeZoneId, string source)
        {
            if (timeZoneId != null)
                FindTimeZone(timeZoneId, source);
        }

        private static TimeZoneInfo FindTimeZone(string timeZoneId, string source)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException)
            {
                throw new InvalidOperationException(
                    $"Time zone '{timeZoneId}' configured for {source} was not found on this host. " +
                    "On .NET Framework only Windows time zone IDs are available.", ex);
            }
        }

        /// <summary>The time zone the handler's cron is evaluated in (UTC when none is set).</summary>
        public TimeZoneInfo TimeZoneOf(RecurringJobRegistryInfo registration)
        {
            var id = Options.TimeZoneIdOf(registration);
            return id == null ? TimeZoneInfo.Utc : FindTimeZone(id, registration.HandlerType.FullName);
        }

        /// <summary>The handler's plan step, or <c>null</c> when it isn't in the plan.</summary>
        public ExecutionPlanStep StepOf(Type handlerType)
        {
            return Plan != null && Plan.TryGet(handlerType, out var step) ? step : null;
        }

        /// <summary>Whether the handler runs: its plan step's resolved state, else the options.</summary>
        public bool IsEnabled(Type handlerType)
        {
            var step = StepOf(handlerType);
            return step?.Enabled ?? Options.IsEnabled(handlerType);
        }

        /// <summary>Whether the handler is a dependent in the plan triggered by its prerequisites (no schedule of its own).</summary>
        public bool IsTriggeredByPlan(Type handlerType)
        {
            var step = StepOf(handlerType);
            return step != null && step.Prerequisites.Count > 0 && !step.RunsOnOwnSchedule;
        }

        /// <summary>Whether the handler is a dependent in the plan running on its own schedule, gated by its prerequisites.</summary>
        public bool IsGatedByPlan(Type handlerType)
        {
            var step = StepOf(handlerType);
            return step != null && step.Prerequisites.Count > 0 && step.RunsOnOwnSchedule;
        }

        /// <summary>How long a run waits for a running one, or <c>null</c> when runs may overlap: the plan step, else the attribute.</summary>
        public TimeSpan? OverlapTimeoutOf(Type handlerType)
        {
            var step = StepOf(handlerType);
            if (step != null)
                return step.OverlapTimeout;

            return RecurringHandlers.TryGetValue(handlerType, out var registration) ? registration.OverlapTimeout : null;
        }
    }
}
