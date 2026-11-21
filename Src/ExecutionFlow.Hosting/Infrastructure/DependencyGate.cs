using ExecutionFlow.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// The plan's generation rule (010 research R-003) in memory: each completed cycle of a handler increments its generation,
    /// and a dependent runs only when every prerequisite has a generation newer than the one it consumed last. Everything
    /// happens under one lock, so concurrent completions trigger a dependent once. Generations start at zero on every start.
    /// </summary>
    internal sealed class DependencyGate
    {
        private readonly object _lock = new object();
        private readonly ExecutionPlan _plan;
        private readonly Dictionary<Type, long> _generations = new Dictionary<Type, long>();
        private readonly Dictionary<Type, Dictionary<Type, long>> _consumed = new Dictionary<Type, Dictionary<Type, long>>();
        private readonly HashSet<Type> _running = new HashSet<Type>();

        public DependencyGate(ExecutionPlan plan)
        {
            _plan = plan;
        }

        /// <summary>
        /// Records a completed cycle of <paramref name="handlerType"/> and returns the dependents triggered by it: enabled,
        /// triggered by their prerequisites (not on their own schedule), not running, and with a new cycle of every prerequisite.
        /// They're marked running and their generations consumed.
        /// </summary>
        public IReadOnlyList<Type> Complete(Type handlerType)
        {
            if (_plan == null || !_plan.TryGet(handlerType, out var step))
                return Array.Empty<Type>();

            lock (_lock)
            {
                _generations[handlerType] = GenerationOf(handlerType) + 1;

                var triggered = new List<Type>();
                foreach (var dependentType in step.Dependents)
                {
                    var dependent = _plan.Get(dependentType);
                    if (dependent.Enabled && !dependent.RunsOnOwnSchedule && !_running.Contains(dependentType) && IsReady(dependent))
                    {
                        Begin(dependent);
                        triggered.Add(dependentType);
                    }
                }

                return triggered;
            }
        }

        /// <summary>
        /// For a dependent on its own schedule: starts a run when every prerequisite has a new cycle and it isn't running.
        /// Otherwise returns <c>false</c> with the reason.
        /// </summary>
        public bool TryBegin(Type dependentType, out string reason)
        {
            var dependent = _plan.Get(dependentType);
            lock (_lock)
            {
                if (_running.Contains(dependentType))
                {
                    reason = "its previous run is still running";
                    return false;
                }

                var pending = dependent.Prerequisites.Where(p => GenerationOf(p) <= ConsumedOf(dependentType, p)).ToList();
                if (pending.Count > 0)
                {
                    reason = "waiting for a new cycle of " + string.Join(", ", pending.Select(p => $"'{p.FullName}'"));
                    return false;
                }

                Begin(dependent);
                reason = null;
                return true;
            }
        }

        /// <summary>
        /// Ends a dependent's run. A dependent triggered by its prerequisites whose prerequisites completed new cycles meanwhile
        /// is started again right away (returns <c>true</c>), instead of having overlapped.
        /// </summary>
        public bool Finish(Type dependentType)
        {
            var dependent = _plan.Get(dependentType);
            lock (_lock)
            {
                _running.Remove(dependentType);

                if (!dependent.RunsOnOwnSchedule && dependent.Enabled && IsReady(dependent))
                {
                    Begin(dependent);
                    return true;
                }

                return false;
            }
        }

        private void Begin(ExecutionPlanStep dependent)
        {
            if (!_consumed.TryGetValue(dependent.HandlerType, out var consumed))
                _consumed[dependent.HandlerType] = consumed = new Dictionary<Type, long>();

            foreach (var prerequisite in dependent.Prerequisites)
                consumed[prerequisite] = GenerationOf(prerequisite);

            _running.Add(dependent.HandlerType);
        }

        private bool IsReady(ExecutionPlanStep dependent)
        {
            return dependent.Prerequisites.All(p => GenerationOf(p) > ConsumedOf(dependent.HandlerType, p));
        }

        private long GenerationOf(Type handlerType)
        {
            return _generations.TryGetValue(handlerType, out var generation) ? generation : 0;
        }

        private long ConsumedOf(Type dependentType, Type prerequisite)
        {
            return _consumed.TryGetValue(dependentType, out var consumed) && consumed.TryGetValue(prerequisite, out var generation) ? generation : 0;
        }
    }
}
