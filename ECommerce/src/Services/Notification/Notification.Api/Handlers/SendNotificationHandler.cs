using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Api.Domain;
using Notification.Api.Infrastructure;
using Notification.Api.Senders;

namespace Notification.Api.Handlers;

public sealed class RetryOptions
{
    public const string SectionName = "Notification:Retry";

    public int MaxAttempts { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 500;
}

public sealed class NotificationDeliveryException(string message) : PermanentMessageException(message);

public sealed class SendNotificationHandler(
    NotificationDbContext db, IEmailSender email, ISmsSender sms, IOptions<RetryOptions> retry,
    ILogger<SendNotificationHandler> logger)
{
    /// <summary>
    /// Önce kanal başına bildirim kaydı oluşturur (idempotent), sonra gönderilmemiş kayıtları sınırlı retry ile gönderir.
    /// Gönderilemeyen kayıt Failed kalır ve istisna fırlatılır (mesaj DLQ'ya gider); replay'de yalnızca Failed/Pending kayıtlar tekrar denenir.
    /// </summary>
    public async Task HandleAsync(OrderResultNotification n, CancellationToken ct)
    {
        var (subject, body) = BuildContent(n);
        var now = DateTime.UtcNow;

        foreach (var channel in NotificationChannel.All)
        {
            var recipient = channel == NotificationChannel.Email
                ? $"customer-{n.CustomerId}@example.invalid"
                : $"customer:{n.CustomerId}";

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO notifications (id, message_id, correlation_id, order_id, customer_id, event_type, channel,
                                            recipient, subject, body, status, attempts, created_at_utc)
                 VALUES ({Guid.NewGuid()}, {n.MessageId}, {n.CorrelationId}, {n.OrderId}, {n.CustomerId}, {n.EventType},
                         {channel}, {recipient}, {subject}, {body}, {NotificationStatus.Pending}, 0, {now})
                 ON CONFLICT (message_id, channel) DO NOTHING
                 """, ct);
        }

        // Kilitli satırlar başka bir worker tarafından işleniyor demektir; atlanır.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Notifications
            .FromSqlInterpolated($"""
                                  SELECT * FROM notifications
                                  WHERE message_id = {n.MessageId} AND status <> {NotificationStatus.Sent}
                                  FOR UPDATE SKIP LOCKED
                                  """)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            await SendWithRetryAsync(row, ct);
        }

        await tx.CommitAsync(ct);

        var failed = rows.Where(r => r.Status == NotificationStatus.Failed).Select(r => r.Channel).ToList();
        if (failed.Count > 0)
        {
            throw new NotificationDeliveryException(
                $"Notification for message {n.MessageId} failed on channel(s): {string.Join(", ", failed)}.");
        }

        if (rows.Count == 0)
        {
            logger.LogInformation("Nothing to send for message {MessageId} (already sent or in progress)", n.MessageId);
        }
    }

    private async Task SendWithRetryAsync(NotificationRecord row, CancellationToken ct)
    {
        var options = retry.Value;
        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            row.Attempts++;
            try
            {
                if (row.Channel == NotificationChannel.Email)
                {
                    await email.SendAsync(row.Recipient, row.Subject, row.Body, ct);
                }
                else
                {
                    await sms.SendAsync(row.Recipient, row.Body, ct);
                }

                row.Status = NotificationStatus.Sent;
                row.SentAtUtc = DateTime.UtcNow;
                row.LastError = null;
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Notification sent {Channel} {OrderId} {MessageId} {CorrelationId} Attempt={Attempt}",
                    row.Channel, row.OrderId, row.MessageId, row.CorrelationId, attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                row.Status = attempt == options.MaxAttempts ? NotificationStatus.Failed : NotificationStatus.Pending;
                await db.SaveChangesAsync(ct);
                logger.LogWarning(ex, "Notification attempt failed {Channel} {OrderId} {MessageId} Attempt={Attempt}",
                    row.Channel, row.OrderId, row.MessageId, attempt);

                if (attempt < options.MaxAttempts && options.BaseDelayMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(options.BaseDelayMs * Math.Pow(2, attempt - 1)), ct);
                }
            }
        }
    }

    private static (string Subject, string Body) BuildContent(OrderResultNotification n) =>
        n.EventType == Messaging.EventTypes.OrderConfirmed
            ? ("Siparişiniz onaylandı", $"{n.OrderId} numaralı siparişiniz onaylandı.")
            : ("Siparişiniz reddedildi",
                $"{n.OrderId} numaralı siparişiniz reddedildi. Neden: {(n.ReasonCode == "INSUFFICIENT_STOCK" ? "yetersiz stok" : n.ReasonCode)}.");
}
