using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Scanner
{
    public static class ExecutionFlowScanner
    {
        public static IReadOnlyList<HandlerRegistration> ScanAssembly(Assembly assembly)
        {
            var registrations = new List<HandlerRegistration>();

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                var recurringAttr = type.GetCustomAttribute<RecurringAttribute>();
                var displayNameAttr = type.GetCustomAttribute<DisplayNameAttribute>();
                var displayName = displayNameAttr?.DisplayName ?? type.Name;
                var cron = recurringAttr?.Cron;

                // Check for IHandler (non-generic, recurring)
                if (typeof(IHandler).IsAssignableFrom(type))
                {
                    registrations.Add(new HandlerRegistration(
                        handlerType: type,
                        eventType: null,
                        displayName: displayName,
                        cron: cron
                    ));
                    continue;
                }

                // Check for IHandler<TEvent>
                var handlerInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>));

                if (handlerInterface != null)
                {
                    var eventType = handlerInterface.GetGenericArguments()[0];
                    registrations.Add(new HandlerRegistration(
                        handlerType: type,
                        eventType: eventType,
                        displayName: displayName,
                        cron: cron
                    ));
                }
            }

            return registrations;
        }
    }
}