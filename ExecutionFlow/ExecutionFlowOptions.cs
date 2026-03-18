using System;
using System.Collections.Generic;
using System.Reflection;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Scanner;

namespace ExecutionFlow
{
    public abstract class ExecutionFlowOptions
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
            var recurringAttr = handlerType.GetCustomAttribute<Attributes.RecurringAttribute>();
            var displayNameAttr = handlerType.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
            var displayName = displayNameAttr?.DisplayName ?? handlerType.Name;
            var cron = recurringAttr?.Cron;

            // Check for IHandler (non-generic)
            if (typeof(IHandler).IsAssignableFrom(handlerType))
            {
                _registrations.Add(new HandlerRegistration(
                    handlerType: handlerType,
                    eventType: null,
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
                        eventType: eventType,
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

        protected void ThrowIfLocked()
        {
            if (_locked)
                throw new InvalidOperationException("Options cannot be modified after Configure has completed.");
        }
    }
}
