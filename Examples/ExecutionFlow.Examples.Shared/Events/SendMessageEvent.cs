using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Shared.Events;

public class SendMessageEvent : ICustomNameEvent
{
    string ICustomNameEvent.CustomName => $"{From}'s message";

    public string From { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
