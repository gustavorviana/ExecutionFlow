using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;
using System;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Filters
{
    public class HangfireAutoRunFilter : IElectStateFilter, IClientFilter
    {
        private readonly bool _autoRunRecurring;
        private readonly IReadOnlyDictionary<Type, bool> _perJobAutoRun;

        public HangfireAutoRunFilter(bool autoRunRecurring, IReadOnlyDictionary<Type, bool> perJobAutoRun)
        {
            _autoRunRecurring = autoRunRecurring;
            _perJobAutoRun = perJobAutoRun ?? new Dictionary<Type, bool>();
        }

        public void OnCreating(CreatingContext context)
        {
            if (!IsAutoScheduledRecurringJob(context.Parameters))
                return;

            if (ShouldBlock(GetHandlerType(context.Job)))
                context.Canceled = true;
        }

        public void OnCreated(CreatedContext context) { }

        // Safety net: covers edge cases that bypass OnCreating
        public void OnStateElection(ElectStateContext context)
        {
            if (!(context.CandidateState is ProcessingState))
                return;

            if (!IsRecurringJob(context) || IsManuallyTriggered(context))
                return;

            if (ShouldBlock(GetHandlerType(context.BackgroundJob.Job)))
                context.CandidateState = new DeletedState();
        }

        private bool ShouldBlock(Type handlerType)
        {
            if (!_autoRunRecurring)
                return true;

            return handlerType != null
                && _perJobAutoRun.TryGetValue(handlerType, out var autoRun)
                && !autoRun;
        }

        private static bool IsAutoScheduledRecurringJob(IDictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("RecurringJobId", out var id) || string.IsNullOrEmpty(id?.ToString()))
                return false;

            return !parameters.TryGetValue("Triggered", out var triggered) || triggered?.ToString() != "1";
        }

        private static bool IsRecurringJob(ElectStateContext context)
        {
            try
            {
                var id = context.Connection.GetJobParameter(context.BackgroundJob.Id, "RecurringJobId");
                return !string.IsNullOrEmpty(id);
            }
            catch { return false; }
        }

        private static bool IsManuallyTriggered(ElectStateContext context)
        {
            try
            {
                return context.Connection.GetJobParameter(context.BackgroundJob.Id, "Triggered") == "1";
            }
            catch { return false; }
        }

        private static Type GetHandlerType(Job job)
        {
            if (job?.Args == null) return null;

            foreach (var arg in job.Args)
            {
                if (arg is Type type)
                    return type;

                if (arg is string typeName && !string.IsNullOrEmpty(typeName))
                {
                    try
                    {
                        var resolved = Type.GetType(typeName);
                        if (resolved != null) return resolved;
                    }
                    catch { }
                }
            }

            return null;
        }
    }
}