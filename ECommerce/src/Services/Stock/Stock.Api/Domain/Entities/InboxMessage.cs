namespace Stock.Api.Domain.Entities;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}
