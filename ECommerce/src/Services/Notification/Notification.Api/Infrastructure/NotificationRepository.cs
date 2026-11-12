using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Notification.Api.Domain;

namespace Notification.Api.Infrastructure;

public interface INotificationRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);

    /// <summary>(MessageId, Channel) için kayıt yoksa Pending olarak ekler; varsa dokunmaz (idempotent).</summary>
    Task CreateIfNotExistsAsync(NotificationRecord record, CancellationToken ct);

    /// <summary>Mesaja ait gönderilmemiş kayıtları kilitler; başka worker'ın kilitlediği satırlar atlanır.</summary>
    Task<List<NotificationRecord>> LockUnsentAsync(Guid messageId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(ct);

    public async Task CreateIfNotExistsAsync(NotificationRecord r, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO notifications (id, message_id, correlation_id, order_id, customer_id, event_type, channel,
                                        recipient, subject, body, status, attempts, created_at_utc)
             VALUES ({r.Id}, {r.MessageId}, {r.CorrelationId}, {r.OrderId}, {r.CustomerId}, {r.EventType},
                     {r.Channel}, {r.Recipient}, {r.Subject}, {r.Body}, {NotificationStatus.Pending}, 0, {r.CreatedAtUtc})
             ON CONFLICT (message_id, channel) DO NOTHING
             """, ct);

    public Task<List<NotificationRecord>> LockUnsentAsync(Guid messageId, CancellationToken ct) =>
        db.Notifications
            .FromSqlInterpolated($"""
                                  SELECT * FROM notifications
                                  WHERE message_id = {messageId} AND status <> {NotificationStatus.Sent}
                                  FOR UPDATE SKIP LOCKED
                                  """)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
