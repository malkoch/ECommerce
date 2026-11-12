namespace Messaging;

public interface IMessagePublisher
{
    /// <summary>Mesajı persistent olarak yayınlar; broker onayı (publisher confirm) gelene kadar bekler.</summary>
    Task PublishAsync(string routingKey, Guid messageId, Guid correlationId, string eventType, string json, CancellationToken ct);
}
