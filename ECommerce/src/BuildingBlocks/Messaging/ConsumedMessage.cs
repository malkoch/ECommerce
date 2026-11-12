using System.Text.Json;

namespace Messaging;

public sealed record ConsumedMessage(string Body, int DeliveryAttempt = 1)
{
    public EventEnvelope<TPayload> ReadEnvelope<TPayload>() =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(Body, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException("Message body is not a valid event envelope.");
}
