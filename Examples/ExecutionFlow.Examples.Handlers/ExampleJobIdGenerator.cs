using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Handlers
{
    /// <summary>
    /// Recurring job IDs without the examples' root namespace: "Recurring.Plan.ProductSyncHandler" instead of
    /// "ExecutionFlow.Examples.Handlers.Recurring.Plan.ProductSyncHandler". Types outside that namespace keep their full name.
    /// </summary>
    public class ExampleJobIdGenerator : IJobIdGenerator
    {
        private const string RootNamespace = "ExecutionFlow.Examples.Handlers.";

        public string GenerateId(Type type)
        {
            var fullName = type.FullName!;

            return fullName.StartsWith(RootNamespace, StringComparison.Ordinal)
                ? fullName.Substring(RootNamespace.Length)
                : fullName;
        }
    }
}
