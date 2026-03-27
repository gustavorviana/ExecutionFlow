using System;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Dispatches events for background processing via fire-and-forget or delayed scheduling.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Publishes an event for immediate background processing.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event instance to publish.</param>
        /// <returns>A <see cref="PublishResult"/> indicating whether the job was enqueued and its ID.</returns>
        PublishResult Publish<TEvent>(TEvent @event);

        /// <summary>
        /// Schedules an event for background processing after a delay.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event instance to schedule.</param>
        /// <param name="delay">The time to wait before processing the event.</param>
        /// <returns>A <see cref="PublishResult"/> indicating whether the job was enqueued and its ID.</returns>
        PublishResult Schedule<TEvent>(TEvent @event, TimeSpan delay);

        /// <summary>
        /// Schedules an event for background processing at a specific time.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event instance to schedule.</param>
        /// <param name="enqueueAt">The date and time when the event should be processed.</param>
        /// <returns>A <see cref="PublishResult"/> indicating whether the job was enqueued and its ID.</returns>
        PublishResult Schedule<TEvent>(TEvent @event, DateTimeOffset enqueueAt);
    }
}
