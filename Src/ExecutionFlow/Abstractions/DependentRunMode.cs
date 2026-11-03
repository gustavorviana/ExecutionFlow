namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// How a dependent in the execution plan runs, other than the default (triggered as soon as every prerequisite completed
    /// a new cycle).
    /// </summary>
    public enum DependentRunMode
    {
        /// <summary>
        /// Triggered by its prerequisites, but never less than an interval after its previous run finished. A trigger that
        /// comes too early is postponed to the end of the interval, not lost.
        /// </summary>
        MinInterval,

        /// <summary>
        /// Runs on its own schedule. Each occurrence runs only if every prerequisite completed a new cycle; otherwise it
        /// records that its prerequisites weren't met.
        /// </summary>
        OwnSchedule
    }
}
