using Notification.Api.Domain.Constants;

namespace Notification.Api.Domain.Entities;

public sealed class NotificationRecord
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string EventType { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string Recipient { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Status { get; set; } = NotificationStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
