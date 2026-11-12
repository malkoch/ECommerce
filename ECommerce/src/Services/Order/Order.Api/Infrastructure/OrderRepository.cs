using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderEntity = Order.Api.Domain.Order;

namespace Order.Api.Infrastructure;

public interface IOrderRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);

    /// <summary>Mesajı inbox'a yazar; daha önce işlenmişse false döner.</summary>
    Task<bool> TryRegisterInboxMessageAsync(Guid messageId, string eventType, CancellationToken ct);

    /// <summary>Siparişi satır kilidiyle yükler; aynı sipariş için eşzamanlı işlemler sıraya girer.</summary>
    Task<OrderEntity?> GetForUpdateAsync(Guid orderId, CancellationToken ct);

    Task<OrderEntity?> GetWithItemsAsync(Guid orderId, CancellationToken ct);

    void Add(OrderEntity order);
    void AddOutboxMessage(OutboxMessage message);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(ct);

    public async Task<bool> TryRegisterInboxMessageAsync(Guid messageId, string eventType, CancellationToken ct)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO inbox_messages (message_id, event_type, processed_at_utc)
             VALUES ({messageId}, {eventType}, {DateTime.UtcNow})
             ON CONFLICT (message_id) DO NOTHING
             """, ct);
        return rows > 0;
    }

    public Task<OrderEntity?> GetForUpdateAsync(Guid orderId, CancellationToken ct) =>
        db.Orders
            .FromSqlInterpolated($"""SELECT * FROM orders WHERE "Id" = {orderId} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);

    public Task<OrderEntity?> GetWithItemsAsync(Guid orderId, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public void Add(OrderEntity order) => db.Orders.Add(order);

    public void AddOutboxMessage(OutboxMessage message) => db.OutboxMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
