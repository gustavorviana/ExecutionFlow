using System;
using System.Collections.Generic;
using System.Reflection;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Scanner;

namespace ExecutionFlow
{
    public class ExecutionFlowOptions
    {
        private readonly List<HandlerRegistration> _registrations = new List<HandlerRegistration>();
        private bool _locked;

        public IReadOnlyList<HandlerRegistration> Registrations => _registrations;

        public void Scan(Assembly assembly)
        {
            ThrowIfLocked();
            var scanned = ExecutionFlowScanner.ScanAssembly(assembly);
            _registrations.AddRange(scanned);
        }

        public void Add(Type handlerType)
        {
            ThrowIfLocked();
            // Scan a single type by checking what interfaces it implements
            var recurringAttr = handlerType.GetCustomAttribute<Attributes.RecurringAttribute>();
            var displayNameAttr = handlerType.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
            var displayName = displayNameAttr?.DisplayName ?? handlerType.Name;
            var isRecurring = recurringAttr != null;
            var cron = recurringAttr?.Cron;

            // Check for IHandler (non-generic)
            if (typeof(IHandler).IsAssignableFrom(handlerType))
            {
                _registrations.Add(new HandlerRegistration(
                    handlerType: handlerType,
                    jobType: null,
                    serviceType: typeof(IHandler),
                    isRecurring: isRecurring,
                    displayName: displayName,
                    cron: cron
                ));
                return;
            }

            // Check for IHandler<TEvent>
            foreach (var iface in handlerType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandler<>))
                {
                    var eventType = iface.GetGenericArguments()[0];
                    _registrations.Add(new HandlerRegistration(
                        handlerType: handlerType,
                        jobType: eventType,
                        serviceType: iface,
                        isRecurring: isRecurring,
                        displayName: displayName,
                        cron: cron
                    ));
                    return;
                }
            }
        }

        internal void Lock()
        {
            _locked = true;
        }

        private void ThrowIfLocked()
        {
            if (_locked)
                throw new InvalidOperationException("ExecutionFlowOptions cannot be modified after Configure has completed.");
        }
    }
}
