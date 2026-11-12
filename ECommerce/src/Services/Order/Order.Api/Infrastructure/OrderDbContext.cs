using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;

namespace Order.Api.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<Domain.Order> Orders => Set<Domain.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Domain.Order>(e =>
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

        b.ConfigureOutbox();
    }
}
