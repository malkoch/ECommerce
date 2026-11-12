using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Stock.Api.Domain;

namespace Stock.Api.Infrastructure;

public interface IStockRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<bool> TryRegisterInboxMessageAsync(Guid messageId, string eventType, CancellationToken ct);

    /// <summary>Sipariş için Pending rezervasyon açar; sipariş zaten rezerve edilmişse false döner.</summary>
    Task<bool> TryCreateReservationAsync(Guid reservationId, Guid orderId, CancellationToken ct);

    /// <summary>Müsait birimlerden (SKIP LOCKED) en fazla quantity kadarını rezervasyona bağlar; bağlanan sayıyı döner.</summary>
    Task<int> ClaimUnitsAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct);

    Task<int> CountFreeUnitsAsync(Guid productId, CancellationToken ct);
    Task MarkReservationFailedAsync(Guid reservationId, string reasonCode, CancellationToken ct);
    Task MarkReservationSucceededAsync(Guid reservationId, IEnumerable<(Guid ProductId, int Quantity)> items, CancellationToken ct);

    Task<string?> GetProductNameAsync(Guid productId, CancellationToken ct);
    Task AddProductAsync(Guid productId, string name, CancellationToken ct);
    Task AddUnitsAsync(Guid productId, int quantity, CancellationToken ct);
    Task<(int Available, int Reserved)> GetStockCountsAsync(Guid productId, CancellationToken ct);

    void AddOutboxMessage(OutboxMessage message);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class StockRepository(StockDbContext db) : IStockRepository
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

    public async Task<bool> TryCreateReservationAsync(Guid reservationId, Guid orderId, CancellationToken ct)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO stock_reservations (id, order_id, status, created_at_utc)
             VALUES ({reservationId}, {orderId}, {ReservationStatus.Pending}, {DateTime.UtcNow})
             ON CONFLICT (order_id) DO NOTHING
             """, ct);
        return rows > 0;
    }

    public Task<int> ClaimUnitsAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE stock_units SET reservation_id = {reservationId}
             WHERE id IN (
                 SELECT id FROM stock_units
                 WHERE product_id = {productId} AND reservation_id IS NULL
                 LIMIT {quantity}
                 FOR UPDATE SKIP LOCKED)
             """, ct);

    public Task<int> CountFreeUnitsAsync(Guid productId, CancellationToken ct) =>
        db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM stock_units WHERE product_id = {productId} AND reservation_id IS NULL")
            .SingleAsync(ct);

    public async Task MarkReservationFailedAsync(Guid reservationId, string reasonCode, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stock_reservations SET status = {ReservationStatus.Failed}, reason_code = {reasonCode} WHERE id = {reservationId}",
            ct);

    public async Task MarkReservationSucceededAsync(
        Guid reservationId, IEnumerable<(Guid ProductId, int Quantity)> items, CancellationToken ct)
    {
        foreach (var (productId, quantity) in items)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO stock_reservation_items (reservation_id, product_id, quantity) VALUES ({reservationId}, {productId}, {quantity})",
                ct);
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stock_reservations SET status = {ReservationStatus.Succeeded} WHERE id = {reservationId}", ct);
    }

    public Task<string?> GetProductNameAsync(Guid productId, CancellationToken ct) =>
        db.StockItems.AsNoTracking().Where(x => x.ProductId == productId).Select(x => x.Name).FirstOrDefaultAsync(ct);

    public async Task AddProductAsync(Guid productId, string name, CancellationToken ct)
    {
        db.StockItems.Add(new StockItem { ProductId = productId, Name = name });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddUnitsAsync(Guid productId, int quantity, CancellationToken ct)
    {
        db.StockUnits.AddRange(Enumerable.Range(0, quantity)
            .Select(_ => new StockUnit { Id = Guid.NewGuid(), ProductId = productId }));
        await db.SaveChangesAsync(ct);
    }

    public async Task<(int Available, int Reserved)> GetStockCountsAsync(Guid productId, CancellationToken ct)
    {
        var counts = await db.StockUnits.AsNoTracking()
            .Where(u => u.ProductId == productId)
            .GroupBy(u => u.ReservationId == null)
            .Select(g => new { Free = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return (counts.FirstOrDefault(c => c.Free)?.Count ?? 0, counts.FirstOrDefault(c => !c.Free)?.Count ?? 0);
    }

    public void AddOutboxMessage(OutboxMessage message) => db.OutboxMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
