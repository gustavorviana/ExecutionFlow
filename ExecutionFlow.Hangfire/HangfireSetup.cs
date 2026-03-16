using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExecutionFlow.Abstractions.Events;
using ExecutionFlow.Hangfire.Dispatcher;
using ExecutionFlow.Hangfire.Filters;
using Hangfire;

namespace ExecutionFlow.Hangfire
{
    public static class HangfireSetup
    {
        public static void Configure(Action<HangfireOptions> configure)
        {
            var options = new HangfireOptions();
            configure(options);
            options.Lock();

            var registrations = ExecutionFlowSetup.Registrations;

            // Validate SetJobAutoRun references
            foreach (var kvp in options.JobAutoRunSettings)
            {
                var handlerType = kvp.Key;
                var isRegistered = registrations.Any(r => r.IsRecurring && r.HandlerType == handlerType);
                if (!isRegistered)
                    throw new InvalidOperationException(
                        $"SetJobAutoRun references type '{handlerType.FullName}' which is not registered as a recurring handler.");
            }

            // Instantiate state handlers via JobActivator
            var activator = JobActivator.Current;
            var stateHandlerInstances = new List<object>();

            foreach (var type in options.StateHandlerTypes)
            {
                object instance;
                try
                {
                    instance = activator.ActivateJob(type);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to activate state handler type '{type.FullName}' via JobActivator.", ex);
                }

                if (instance == null)
                    throw new InvalidOperationException(
                        $"Failed to activate state handler type '{type.FullName}' via JobActivator.");

                stateHandlerInstances.Add(instance);
            }

            // Group state handlers by interface
            var onEnqueued = stateHandlerInstances.OfType<IOnEnqueued>().ToList();
            var onProcessing = stateHandlerInstances.OfType<IOnProcessing>().ToList();
            var onSucceeded = stateHandlerInstances.OfType<IOnSucceeded>().ToList();
            var onFailed = stateHandlerInstances.OfType<IOnFailed>().ToList();
            var onCancelled = stateHandlerInstances.OfType<IOnCancelled>().ToList();
            var onRetrying = stateHandlerInstances.OfType<IOnRetrying>().ToList();

            // Register HangfireStateFilter
            var stateFilter = new HangfireStateFilter(
                onEnqueued, onProcessing, onSucceeded, onFailed, onCancelled, onRetrying);
            GlobalJobFilters.Filters.Add(stateFilter);

            // Register HangfireAutoRunFilter
            var autoRunFilter = new HangfireAutoRunFilter(
                options.AutoRunRecurring, options.JobAutoRunSettings);
            GlobalJobFilters.Filters.Add(autoRunFilter);

            // Register recurring jobs
            foreach (var registration in registrations.Where(r => r.IsRecurring))
            {
                var handlerTypeName = registration.HandlerType.AssemblyQualifiedName;
                var displayName = registration.DisplayName;

                RecurringJob.AddOrUpdate<HangfireJobDispatcher>(
                    registration.DisplayName,
                    dispatcher => dispatcher.DispatchRecurringAsync(
                        displayName, null, handlerTypeName, CancellationToken.None),
                    registration.Cron);
            }
        }
    }
}
