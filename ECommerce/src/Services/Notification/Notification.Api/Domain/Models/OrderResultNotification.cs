namespace Notification.Api.Domain.Models;

/// <summary>OrderConfirmed / OrderRejected olaylarının bildirim için ortak özeti.</summary>
public sealed record OrderResultNotification(
    Guid MessageId, Guid CorrelationId, string EventType, Guid OrderId, Guid CustomerId, string? ReasonCode);
