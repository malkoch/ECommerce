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
            throw new InvalidOperationException($"Invalid OrderCreated payload for message {envelope.MessageId}.");
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

        var reservationId = Guid.NewGuid();
        var reservationRows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO stock_reservations (id, order_id, status, created_at_utc)
             VALUES ({reservationId}, {order.OrderId}, {ReservationStatus.Pending}, {now})
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
            }
        }

        if (failedProducts.Count > 0)
        {
            // Kısmi düşüm kalmaması için bu siparişin tüm stok güncellemeleri geri alınır.
            await tx.RollbackToSavepointAsync("reserve", ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_reservations SET status = {ReservationStatus.Failed}, reason_code = {InsufficientStock} WHERE id = {reservationId}",
                ct);

            var failed = EventEnvelope<StockReservationFailedPayload>.Create(EventTypes.StockReservationFailed,
                new StockReservationFailedPayload(order.OrderId, InsufficientStock, failedProducts), envelope.CorrelationId);
            db.OutboxMessages.Add(OutboxModelBuilderExtensions.CreateOutboxMessage(failed, Topology.RoutingKeys.StockReservationFailed));
        }
        else
        {
            foreach (var (productId, quantity) in items)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO stock_reservation_items (reservation_id, product_id, quantity) VALUES ({reservationId}, {productId}, {quantity})",
                    ct);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_reservations SET status = {ReservationStatus.Succeeded} WHERE id = {reservationId}", ct);

            var succeeded = EventEnvelope<StockReservationSucceededPayload>.Create(EventTypes.StockReservationSucceeded,
                new StockReservationSucceededPayload(order.OrderId, reservationId), envelope.CorrelationId);
            db.OutboxMessages.Add(OutboxModelBuilderExtensions.CreateOutboxMessage(succeeded, Topology.RoutingKeys.StockReservationSucceeded));
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var outcome = failedProducts.Count > 0 ? ReservationOutcome.Failed : ReservationOutcome.Succeeded;
        logger.LogInformation("Stock reservation {Outcome} for {OrderId} {MessageId} {CorrelationId}",
            outcome, order.OrderId, envelope.MessageId, envelope.CorrelationId);
        return outcome;
    }
}
