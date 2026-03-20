using System.ComponentModel;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using ExecutionFlow.Hangfire.Console;

namespace ExecutionFlow.Examples.Handlers.Recurring;

[Recurring("*/5 * * * *")]
[DisplayName("Data Sync")]
public class DataSyncHandler : IHandler
{
    public async Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        const int totalItems = 10;

        context.Log.Info("Starting data synchronization...");

        var progressBar = context.CreateProgressBar("Syncing records");

        for (var i = 1; i <= totalItems; i++)
        {
            // Simulate processing each record
            await Task.Delay(500, cancellationToken);

            context.Log.Info($"Processed record {i}/{totalItems}");
            progressBar.SetValue(i, totalItems);
        }

        progressBar.Complete();
        context.Log.Success("Data synchronization completed.");
    }
}
