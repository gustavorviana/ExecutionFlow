namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// What <see cref="ExecutionPlanner.Build(MissingPrerequisite)"/> does when an enabled handler depends on a prerequisite
    /// that isn't available: it wasn't added to the planner, or it was added with <see cref="ExecutionPlanEntry.Enabled(bool)"/> <c>false</c>.
    /// </summary>
    public enum MissingPrerequisite
    {
        /// <summary>
        /// Throw <see cref="System.InvalidOperationException"/>. A dependent waiting for a prerequisite that never runs would
        /// stop silently, so the default is to fail at startup.
        /// </summary>
        Throw = 0,

        /// <summary>
        /// Disable the dependent, with a reason naming the prerequisite. The effect cascades down the chain.
        /// </summary>
        SkipDependents = 1
    }
}
