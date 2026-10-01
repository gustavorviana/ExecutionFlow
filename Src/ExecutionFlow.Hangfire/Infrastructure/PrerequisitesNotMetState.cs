using Hangfire.States;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Final state of a dependent recurring job that was due but didn't run, because a prerequisite hadn't completed a new
    /// cycle since the dependent's previous run (execution plan). It never reaches a worker and fires no lifecycle hook.
    /// </summary>
    internal sealed class PrerequisitesNotMetState : IState
    {
        public const string StateName = "PrerequisitesNotMet";
        public const string PendingPrerequisitesKey = "PendingPrerequisites";

        private readonly string _pendingPrerequisites;

        public PrerequisitesNotMetState(IEnumerable<string> pendingPrerequisites)
        {
            _pendingPrerequisites = string.Join(", ", pendingPrerequisites);
        }

        public string Name => StateName;

        public string Reason => "Waiting for a new completed cycle of: " + _pendingPrerequisites;

        public bool IsFinal => true;

        public bool IgnoreJobLoadException => false;

        public Dictionary<string, string> SerializeData()
        {
            return new Dictionary<string, string>
            {
                [PendingPrerequisitesKey] = _pendingPrerequisites
            };
        }
    }
}
