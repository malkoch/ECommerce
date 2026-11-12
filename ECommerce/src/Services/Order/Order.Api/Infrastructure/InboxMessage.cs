namespace Order.Api.Infrastructure;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}
