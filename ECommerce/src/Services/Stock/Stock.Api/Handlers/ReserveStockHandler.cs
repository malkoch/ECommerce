using Messaging;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain;
using Stock.Api.Infrastructure;

namespace Stock.Api.Handlers;

public enum ReservationOutcome
{
    Succeeded,
    Failed,
    Duplicate
}

public sealed class ReserveStockHandler(StockDbContext db, ILogger<ReserveStockHandler> logger)
{
    public const string InsufficientStock = "INSUFFICIENT_STOCK";

    public async Task<ReservationOutcome> HandleAsync(EventEnvelope<OrderCreatedPayload> envelope, CancellationToken ct)
    {
        var order = envelope.Payload;
        if (order.OrderId == Guid.Empty || order.Items is not { Count: > 0 } || order.Items.Any(i => i.Quantity <= 0))
        {
            throw new PermanentMessageException($"Invalid OrderCreated payload for message {envelope.MessageId}.");
        }

        // Aynı ürün birden fazla kalemde gelirse toplanır; sabit sırada kilitleyerek deadlock riski azaltılır.
        var items = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .OrderBy(i => i.ProductId)
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var inboxRows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO inbox_messages (message_id, event_type, processed_at_utc)
             VALUES ({envelope.MessageId}, {envelope.EventType}, {now})
             ON CONFLICT (message_id) DO NOTHING
             """, ct);
        if (inboxRows == 0)
        {
            logger.LogInformation("Duplicate message {MessageId} ignored", envelope.MessageId);
            return ReservationOutcome.Duplicate;
        }

        // Sıcak ürünlerde satır kilidi commit'e kadar tutulur; bu yüzden stok güncellemeleri transaction'ın
        // en sonuna bırakılır, tüm hazırlık yazımları (rezervasyon, kalemler, outbox) kilitten önce yapılır.
        var reservationId = Guid.NewGuid();
        var reservationRows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO stock_reservations (id, order_id, status, created_at_utc)
             VALUES ({reservationId}, {order.OrderId}, {ReservationStatus.Succeeded}, {now})
             ON CONFLICT (order_id) DO NOTHING
             """, ct);
        if (reservationRows == 0)
        {
            await tx.CommitAsync(ct);
            logger.LogInformation("Order {OrderId} already has a reservation; message {MessageId} ignored",
                order.OrderId, envelope.MessageId);
            return ReservationOutcome.Duplicate;
        }

        await tx.CreateSavepointAsync("reserve", ct);

        foreach (var (productId, quantity) in items)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO stock_reservation_items (reservation_id, product_id, quantity) VALUES ({reservationId}, {productId}, {quantity})",
                ct);
        }

        var succeeded = EventEnvelope<StockReservationSucceededPayload>.Create(EventTypes.StockReservationSucceeded,
            new StockReservationSucceededPayload(order.OrderId, reservationId), envelope.CorrelationId);
        db.OutboxMessages.Add(OutboxModelBuilderExtensions.CreateOutboxMessage(succeeded, Topology.RoutingKeys.StockReservationSucceeded));
        await db.SaveChangesAsync(ct);

        var failedProducts = new List<Guid>();
        foreach (var (productId, quantity) in items)
        {
            var updated = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE stock_items
                 SET available_quantity = available_quantity - {quantity}
                 WHERE product_id = {productId} AND available_quantity >= {quantity}
                 """, ct);
            if (updated == 0)
            {
                failedProducts.Add(productId);
                break;
            }
        }

        if (failedProducts.Count > 0)
        {
            // Savepoint'e dönmek stok düşümlerini, kalemleri ve başarı olayını geri alır; kilitler hemen bırakılır.
            await tx.RollbackToSavepointAsync("reserve", ct);
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_reservations SET status = {ReservationStatus.Failed}, reason_code = {InsufficientStock} WHERE id = {reservationId}",
                ct);

            var failed = EventEnvelope<StockReservationFailedPayload>.Create(EventTypes.StockReservationFailed,
                new StockReservationFailedPayload(order.OrderId, InsufficientStock, failedProducts), envelope.CorrelationId);
            db.OutboxMessages.Add(OutboxModelBuilderExtensions.CreateOutboxMessage(failed, Topology.RoutingKeys.StockReservationFailed));
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);

        var outcome = failedProducts.Count > 0 ? ReservationOutcome.Failed : ReservationOutcome.Succeeded;
        logger.LogInformation("Stock reservation {Outcome} for {OrderId} {MessageId} {CorrelationId}",
            outcome, order.OrderId, envelope.MessageId, envelope.CorrelationId);
        return outcome;
    }
}
