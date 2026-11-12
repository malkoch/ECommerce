namespace Messaging.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = default!;
    public string RoutingKey { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
