using Hangfire.States;
using System.Collections.Generic;

namespace ExecutionFlow.Hangfire.Infrastructure.Filters
{
    internal class AutoStartNotAllowedCanceledState : IState
    {
        public const string StateName = "auto-start-not-allowed-canceled";

        private const string ReasonMessage =
            "Automatic service start attempt was blocked because the operation is not permitted in the current context.";

        public string Name => StateName;

        public string Reason => ReasonMessage;

        public bool IsFinal => true;

        public bool IgnoreJobLoadException => false;

        public Dictionary<string, string> SerializeData()
        {
            return new Dictionary<string, string>
            {
                ["Reason"] = ReasonMessage
            };
        }
    }
}
