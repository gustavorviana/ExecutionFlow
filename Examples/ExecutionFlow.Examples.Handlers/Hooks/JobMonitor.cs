using ExecutionFlow.Abstractions.Events;

namespace ExecutionFlow.Examples.Handlers.Hooks;

/// <summary>Lifecycle hooks: registered with <c>options.AddStateHandler&lt;JobMonitor&gt;()</c>.</summary>
public class JobMonitor : IOnSucceeded, IOnFailed, IOnRetrying, IOnCancelled
{
    public void OnSucceeded(ExecutionSucceededEvent e) =>
        Console.WriteLine($"[hook] {e.HandlerType?.Name} job {e.JobId} succeeded in {e.Duration.TotalMilliseconds:N0} ms");

    public void OnFailed(ExecutionFailedEvent e) =>
        Console.WriteLine($"[hook] {e.HandlerType?.Name} job {e.JobId} ({e.CustomId}) failed for good: {e.Exception?.Message}");

    public void OnRetrying(ExecutionRetryingEvent e) =>
        Console.WriteLine($"[hook] {e.HandlerType?.Name} job {e.JobId} will retry (attempt {e.AttemptNumber})");

    public void OnCancelled(ExecutionEvent e) =>
        Console.WriteLine($"[hook] {e.HandlerType?.Name} job {e.JobId} ({e.CustomId}) cancelled");
}
