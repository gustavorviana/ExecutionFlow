using ExecutionFlow.Abstractions;
using Hangfire.Common;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    public class HangfireEventJobInfo : HangfireJobInfo
    {
        internal const int CustomNameIndex = 1;
        internal const int JobArgSize = 4;

        public Type EventType { get; }
        public string CustomJobName { get; }

        public HangfireEventJobInfo(Job job) : base(job)
        {
            EventType = job.Method.GetGenericArguments()[0];
            var foundName = job.Args?.Count != JobArgSize ? null : job.Args[CustomNameIndex] as string;
            if (!string.IsNullOrEmpty(foundName))
                CustomJobName = foundName;
        }

        public override IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry)
        {
            if (registry.EventHandlers.TryGetValue(EventType, out var eventHandler))
                return eventHandler;

            return null;
        }

        public override string GetExpectedName(IJobRegistryInfo info)
        {
            if (!string.IsNullOrEmpty(CustomJobName))
                return CustomJobName;

            return base.GetExpectedName(info);
        }
    }

    public class HangfireRecurringJobInfo : HangfireJobInfo
    {
        internal const int EventHandlerIndex = 1;
        internal const int JobArgSize = 3;

        public Type HandlerType { get; }

        public HangfireRecurringJobInfo(Job job) : base(job)
        {
            HandlerType = Job?.Args?.Count != JobArgSize ? null : Job.Args[EventHandlerIndex] as Type;
        }

        public override IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry)
        {
            if (registry.RecurringHandlers.TryGetValue(HandlerType, out var eventHandler)) 
                return eventHandler;

            return null;
        }

        public static Type GetJobType(Job job)
        {
            if (job.Args?.Count == JobArgSize && job.Args[RecurringEventType] is Type jobType)
                return jobType;

            return null;
        }
    }

    public abstract class HangfireJobInfo
    {
        internal const int RecurringEventType = 1;

        public Job Job { get; }

        internal HangfireJobInfo(Job job)
        {
            Job = job;
        }

        public static HangfireJobInfo Create(Job job)
        {
            if (job?.Method?.IsGenericMethod == true)
                return new HangfireEventJobInfo(job);

            return new HangfireRecurringJobInfo(job);
        }

        public Type GetHandlerType(IExecutionFlowRegistry registry)
        {
            return GetHandler(registry)?.HandlerType;
        }

        public virtual string GetExpectedName(IExecutionFlowRegistry registry)
        {
            return GetExpectedName(GetHandler(registry));
        }

        public abstract IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry);

        public virtual string GetExpectedName(IJobRegistryInfo info)
        {
            return string.IsNullOrEmpty(info?.DisplayName) ? info?.HandlerType?.FullName : info.DisplayName;
        }
    }
}