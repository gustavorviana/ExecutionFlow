using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// At startup in scheduled mode, logs one line per recurring handler saying whether it was scheduled and, if not, why.
    /// Stopping it cancels every run in progress.
    /// </summary>
    internal sealed class StartupReportHostedService : IHostedService
    {
        private readonly HostingSetup _setup;
        private readonly RunCoordinator _coordinator;
        private readonly ILogger _logger;

        public StartupReportHostedService(HostingSetup setup, RunCoordinator coordinator, ILogger logger)
        {
            _setup = setup;
            _coordinator = coordinator;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var handlerType in _setup.RecurringOrder)
                _logger.LogInformation("ExecutionFlow: {Report}", Describe(handlerType));

            return Task.CompletedTask;
        }

        internal string Describe(Type handlerType)
        {
            var name = handlerType.FullName;
            var step = _setup.StepOf(handlerType);

            if (!_setup.IsEnabled(handlerType))
            {
                if (step == null)
                    return $"'{name}' is not scheduled: disabled (GetPlanOf().Enabled(false) or GlobalRecurringAutoRun).";
                if (step.SkipReason != "disabled")
                    return $"'{name}' is not scheduled: {step.SkipReason} (the plan's SkipDependents).";
                return step.ConfiguredEnabled == false
                    ? $"'{name}' is not scheduled: disabled in the execution plan."
                    : $"'{name}' is not scheduled: disabled (GlobalRecurringAutoRun).";
            }

            if (_setup.IsTriggeredByPlan(handlerType))
            {
                var minInterval = step.MinInterval.HasValue ? $", at least {step.MinInterval.Value} after its previous run" : "";
                return $"'{name}' is triggered by its prerequisites {Names(step)}{minInterval}.";
            }

            var registration = _setup.RecurringHandlers[handlerType];
            var schedule = registration.Interval.HasValue
                ? $"every {registration.Interval.Value}"
                : $"on the cron '{registration.Cron}' ({_setup.TimeZoneOf(registration).Id})";
            var gate = _setup.IsGatedByPlan(handlerType) ? $", after a new cycle of {Names(step)}" : "";

            return $"'{name}' is scheduled {schedule}{gate}.";
        }

        private static string Names(global::ExecutionFlow.Abstractions.ExecutionPlanStep step)
        {
            return string.Join(", ", step.Prerequisites.Select(p => $"'{p.FullName}'"));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _coordinator.StopAsync(cancellationToken);
        }
    }
}
