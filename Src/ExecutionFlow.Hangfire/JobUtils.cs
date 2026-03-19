using ExecutionFlow.Abstractions;
using Hangfire.Common;
using Microsoft.Win32;
using System;

namespace ExecutionFlow.Hangfire
{
    internal static class JobUtils
    {
        public static Type GetHandlerType(this Job job, IExecutionFlowRegistry registry)
        {
            if (job == null)
                return null;

            if (TryGetEventHandler(job, registry, out var eventHandlerType))
                return eventHandlerType;

            return GetRecurringHandlerType(job, registry);
        }

        public static bool TryGetEventHandler(this Job job, IExecutionFlowRegistry registry, out Type eventHandlerType)
        {
            if (!TryGetEventType(job, out var eventType) || !registry.EventHandlers.TryGetValue(eventType, out var eventHandler))
            {
                eventHandlerType = null;
                return false;
            }

            eventHandlerType = eventHandler.HandlerType;
            return true;
        }

        public static bool TryGetEventType(this Job job, out Type eventHandlerType)
        {
            if (!IsEventHandler(job))
            {
                eventHandlerType = null;
                return false;
            }

            eventHandlerType = job.Method.GetGenericArguments()[0];
            return true;
        }

        public static bool IsEventHandler(this Job job)
        {
            return job?.Method?.IsGenericMethod == true;
        }

        public static Type GetRecurringHandlerType(this Job job, IExecutionFlowRegistry registry)
        {
            if (job?.Args == null || job.Args.Count != 3)
                return null;

            return job.Args[1] as Type;
        }
    }
}
