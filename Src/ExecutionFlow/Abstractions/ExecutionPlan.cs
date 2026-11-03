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

        /// <summary>Gets the policy the plan was built with, also used when a provider re-evaluates which handlers are enabled.</summary>
        public MissingPrerequisite MissingPrerequisite { get; }

        internal ExecutionPlan(IList<ExecutionPlanStep> steps, MissingPrerequisite missingPrerequisite)
        {
            MissingPrerequisite = missingPrerequisite;
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

        /// <summary>
        /// Re-resolves which steps run when the provider decides the enabled state of the steps whose builder didn't set it
        /// (its global default), applying <see cref="MissingPrerequisite"/> again: an unavailable prerequisite
        /// throws or turns its dependents off in cascade, as at <see cref="ExecutionPlanner.Build(Abstractions.MissingPrerequisite)"/>.
        /// </summary>
        /// <param name="isEnabledByProvider">The provider's on/off for a handler whose builder didn't call <c>Enabled</c>.</param>
        internal ExecutionPlan Reevaluate(Func<Type, bool> isEnabledByProvider)
        {
            var reasons = new Dictionary<Type, string>(ExecutionPlanner.TypeComparer);
            var steps = new List<ExecutionPlanStep>(Steps.Count);

            // Steps are in execution order, so every prerequisite is resolved before its dependents.
            foreach (var step in Steps)
            {
                var enabled = step.ConfiguredEnabled ?? isEnabledByProvider(step.HandlerType);
                var reason = enabled
                    ? ExecutionPlanner.ResolveUnavailablePrerequisite(step.HandlerType, step.Prerequisites, reasons, MissingPrerequisite)
                    : "disabled";

                reasons.Add(step.HandlerType, reason);
                steps.Add(step.WithSkipReason(reason));
            }

            return new ExecutionPlan(steps, MissingPrerequisite);
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
