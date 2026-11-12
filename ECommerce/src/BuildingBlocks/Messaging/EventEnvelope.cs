namespace Messaging;

public sealed record EventEnvelope<TPayload>(
    Guid MessageId,
    Guid CorrelationId,
    DateTime OccurredAtUtc,
    string EventType,
    int SchemaVersion,
    TPayload Payload)
{
    public static EventEnvelope<TPayload> Create(string eventType, TPayload payload, Guid correlationId) =>
        new(Guid.NewGuid(), correlationId, DateTime.UtcNow, eventType, 1, payload);
}
