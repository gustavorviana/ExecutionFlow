using ExecutionFlow.Abstractions;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Linq;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    public class HangfireAutoRunFilter : IElectStateFilter, IApplyStateFilter
    {
        private static readonly TimeSpan AutoStartNotAllowedCanceledStateExpiration = TimeSpan.FromSeconds(1);
        private static readonly string[] ManualHangfireTrigger = new string[]
        {
            "Triggered using recurring job manager",
            "Triggered via Dashboard UI"
        };

        private const string HangfireTriggeredKey = "Triggered";
        private const string HangfireTriggeredValue = "1";

        private readonly HangfireOptions _options;
        private readonly IExecutionFlowRegistry _handlerRegistry;

        public HangfireAutoRunFilter(IExecutionFlowRegistry handlerRegistry, HangfireOptions options)
        {
            _handlerRegistry = handlerRegistry;
            _options = options;
        }

        public void OnStateElection(ElectStateContext context)
        {
            if (!(context.CandidateState is EnqueuedState state) || !IsRecurringJob(context))
                return;

            var handlerType = HangfireJobInfo.Create(context.BackgroundJob.Job)?.GetHandlerType(_handlerRegistry);
            if (!IsManuallyTriggered(context) && !CanAutoRun(handlerType) && !IsAllowedEnqueue(state))
                context.CandidateState = new AutoStartNotAllowedCanceledState();
        }

        private bool CanAutoRun(Type handlerType)
        {
            if (handlerType == null)
                return false;

            if (_options.RecurringAutoRun.TryGetValue(handlerType, out var autoRun))
                return autoRun;

            return _options.GlobalRecurringAutoRun;
        }

        private static bool IsRecurringJob(ElectStateContext context)
        {
            return context.BackgroundJob?.Job?.IsRecurring() ?? false;
        }

        private static bool IsManuallyTriggered(ElectStateContext context)
        {
            if (IsAllowedEnqueue(context.CandidateState))
                return true;

            if (context.BackgroundJob.ParametersSnapshot.TryGetValue(HangfireTriggeredKey, out var value))
                return value == HangfireTriggeredValue;

            return false;
        }

        private static bool IsAllowedEnqueue(IState state) =>
            ManualHangfireTrigger.Contains(state.Reason, StringComparer.OrdinalIgnoreCase);

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (context.NewState is AutoStartNotAllowedCanceledState)
                context.JobExpirationTimeout = AutoStartNotAllowedCanceledStateExpiration;
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }
    }
}