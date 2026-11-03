using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;

namespace ExecutionFlow.Examples.Shared.Events;

/// <summary>
/// An event with a custom ID (track, cancel and retry it as "order-{id}") and a dashboard name. While a job for the same
/// order is active, publishing again is skipped.
/// </summary>
[Deduplication(DeduplicationBehavior.SkipIfExists)]
public class OrderCreatedEvent : ICustomIdEvent, ICustomNameEvent
{
    public string OrderId { get; set; } = string.Empty;

    public string CustomId => $"order-{OrderId}";

    public string CustomName => $"Order {OrderId}";
}
