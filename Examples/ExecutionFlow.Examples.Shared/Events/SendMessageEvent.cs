namespace ExecutionFlow.Examples.Shared.Events;

public class SendMessageEvent
{
    public string From { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
