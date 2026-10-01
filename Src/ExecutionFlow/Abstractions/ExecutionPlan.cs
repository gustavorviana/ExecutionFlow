using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// The validated, immutable result of <see cref="ExecutionPlanner.Build(MissingPrerequisite)"/>.
    /// </summary>
    /// <remarks>
    /// Separate from the planner on purpose: the planner is where handlers are declared and checked; this is the frozen
    /// result, so nobody can add a dependency after the checks ran.
    /// </remarks>
    public sealed class ExecutionPlan
    {
        private readonly Dictionary<Type, ExecutionPlanStep> _steps;

        /// <summary>
        /// Gets every declared handler, in an order where each prerequisite comes before its dependents, keeping the
        /// declaration order wherever dependencies allow it.
        /// </summary>
        public IReadOnlyList<ExecutionPlanStep> Steps { get; }

        internal ExecutionPlan(IList<ExecutionPlanStep> steps)
        {
            Steps = new ReadOnlyCollection<ExecutionPlanStep>(steps);
            _steps = new Dictionary<Type, ExecutionPlanStep>(ExecutionPlanner.TypeComparer);
            foreach (var step in steps)
                _steps.Add(step.HandlerType, step);
        }

        /// <summary>Gets the step of <typeparamref name="THandler"/>.</summary>
        /// <exception cref="KeyNotFoundException">The handler isn't in the plan.</exception>
        public ExecutionPlanStep Get<THandler>() where THandler : class, IHandler
        {
            return Get(typeof(THandler));
        }

        /// <summary>Gets the step of <paramref name="handlerType"/>.</summary>
        /// <param name="handlerType">The handler type.</param>
        /// <exception cref="KeyNotFoundException">The handler isn't in the plan.</exception>
        public ExecutionPlanStep Get(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            if (!_steps.TryGetValue(handlerType, out var step))
                throw new KeyNotFoundException($"'{handlerType.FullName}' is not in the execution plan.");

            return step;
        }

        /// <summary>Gets the step of <paramref name="handlerType"/>, if it's in the plan.</summary>
        /// <param name="handlerType">The handler type.</param>
        /// <param name="step">The step, or <c>null</c>.</param>
        public bool TryGet(Type handlerType, out ExecutionPlanStep step)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            return _steps.TryGetValue(handlerType, out step);
        }
    }
}
