using Microsoft.EntityFrameworkCore;
using Notification.Api.Domain;

namespace Notification.Api.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.Entity<NotificationRecord>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.MessageId).HasColumnName("message_id");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(16);
            e.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(200);
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200);
            e.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(500);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.SentAtUtc).HasColumnName("sent_at_utc");
            // Aynı olay aynı kanal için tek bildirim üretir (idempotency anahtarı).
            e.HasIndex(x => new { x.MessageId, x.Channel }).IsUnique();
            e.HasIndex(x => x.OrderId);
        });
}
