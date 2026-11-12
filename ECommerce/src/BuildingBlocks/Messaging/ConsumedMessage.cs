using System.Text.Json;

namespace Messaging;

public sealed record ConsumedMessage(string Body, int DeliveryAttempt = 1)
{
    public string EventType
    {
        get
        {
            using var doc = JsonDocument.Parse(Body);
            return doc.RootElement.TryGetProperty("eventType", out var t) && t.GetString() is { } v
                ? v
                : throw new PermanentMessageException("Message has no eventType.");
        }
    }

    public EventEnvelope<TPayload> ReadEnvelope<TPayload>() =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(Body, JsonSerializerOptions.Web)
        ?? throw new PermanentMessageException("Message body is not a valid event envelope.");

    /// <summary>Loglama için zarf metadata'sını best-effort okur; bozuk mesajda boş döner.</summary>
    public (Guid? MessageId, Guid? CorrelationId, string? EventType) PeekMetadata()
    {
        try
        {
            using var doc = JsonDocument.Parse(Body);
            var root = doc.RootElement;
            Guid? Id(string name) =>
                root.TryGetProperty(name, out var p) && Guid.TryParse(p.GetString(), out var g) ? g : null;
            return (Id("messageId"), Id("correlationId"),
                root.TryGetProperty("eventType", out var t) ? t.GetString() : null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return (null, null, null);
        }
    }
}
