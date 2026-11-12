namespace Notification.Api.Domain;

public static class NotificationChannel
{
    public const string Email = "Email";
    public const string Sms = "Sms";
    public static readonly string[] All = [Email, Sms];
}

public static class NotificationStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

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

/// <summary>OrderConfirmed / OrderRejected olaylarının bildirim için ortak özeti.</summary>
public sealed record OrderResultNotification(
    Guid MessageId, Guid CorrelationId, string EventType, Guid OrderId, Guid CustomerId, string? ReasonCode);
