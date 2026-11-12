using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using OrderEntity = Order.Api.Domain.Entities.Order;

namespace Order.Api.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<OrderEntity>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(x => x.Id);
        });

        b.Entity<InboxMessage>(e =>
        {
            e.ToTable("inbox_messages");
            e.HasKey(x => x.MessageId);
            e.Property(x => x.MessageId).HasColumnName("message_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        });

        b.ConfigureOutbox();
    }
}
