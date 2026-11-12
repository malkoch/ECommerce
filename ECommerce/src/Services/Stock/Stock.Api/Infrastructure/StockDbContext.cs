using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain;

namespace Stock.Api.Infrastructure;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options), IOutboxDbContext
{
    public static readonly Guid LaptopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PhoneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HeadphonesId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockUnit> StockUnits => Set<StockUnit>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();
    public DbSet<StockReservationItem> ReservationItems => Set<StockReservationItem>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<StockItem>(e =>
        {
            e.ToTable("stock_items");
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.HasData(
                new StockItem { ProductId = LaptopId, Name = "Laptop" },
                new StockItem { ProductId = PhoneId, Name = "Phone" },
                new StockItem { ProductId = HeadphonesId, Name = "Headphones" });
        });

        b.Entity<StockUnit>(e =>
        {
            e.ToTable("stock_units");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ReservationId).HasColumnName("reservation_id");
            e.HasIndex(x => x.ProductId).HasFilter("reservation_id IS NULL").HasDatabaseName("ix_stock_units_available");
            e.HasIndex(x => x.ReservationId);
        });

        b.Entity<StockReservation>(e =>
        {
            e.ToTable("stock_reservations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(64);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.HasIndex(x => x.OrderId).IsUnique();
        });

        b.Entity<StockReservationItem>(e =>
        {
            e.ToTable("stock_reservation_items");
            e.HasKey(x => new { x.ReservationId, x.ProductId });
            e.Property(x => x.ReservationId).HasColumnName("reservation_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.HasOne<StockReservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
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
