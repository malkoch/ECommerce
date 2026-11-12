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

public sealed class ReserveStockHandler(IStockRepository repository, ILogger<ReserveStockHandler> logger)
{
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    private const int MaxClaimAttempts = 5;

    public async Task<ReservationOutcome> HandleAsync(EventEnvelope<OrderCreatedPayload> envelope, CancellationToken ct)
    {
        var order = envelope.Payload;
        if (order.OrderId == Guid.Empty || order.Items is not { Count: > 0 } || order.Items.Any(i => i.Quantity <= 0))
        {
            throw new PermanentMessageException($"Invalid OrderCreated payload for message {envelope.MessageId}.");
        }

        // Aynı ürün birden fazla kalemde gelirse toplanır.
        var items = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .OrderBy(i => i.ProductId)
            .ToList();

        await using var tx = await repository.BeginTransactionAsync(ct);

        if (!await repository.TryRegisterInboxMessageAsync(envelope.MessageId, envelope.EventType, ct))
        {
            logger.LogInformation("Duplicate message {MessageId} ignored", envelope.MessageId);
            return ReservationOutcome.Duplicate;
        }

        var reservationId = Guid.NewGuid();
        if (!await repository.TryCreateReservationAsync(reservationId, order.OrderId, ct))
        {
            await tx.CommitAsync(ct);
            logger.LogInformation("Order {OrderId} already has a reservation; message {MessageId} ignored",
                order.OrderId, envelope.MessageId);
            return ReservationOutcome.Duplicate;
        }

        var failedProducts = await ClaimUnitsAsync(tx, reservationId, items, ct);

        if (failedProducts.Count > 0)
        {
            await repository.MarkReservationFailedAsync(reservationId, InsufficientStock, ct);

            var failed = EventEnvelope<StockReservationFailedPayload>.Create(EventTypes.StockReservationFailed,
                new StockReservationFailedPayload(order.OrderId, InsufficientStock, failedProducts), envelope.CorrelationId);
            repository.AddOutboxMessage(OutboxModelBuilderExtensions.CreateOutboxMessage(failed, Topology.RoutingKeys.StockReservationFailed));
        }
        else
        {
            await repository.MarkReservationSucceededAsync(reservationId, items, ct);

            var succeeded = EventEnvelope<StockReservationSucceededPayload>.Create(EventTypes.StockReservationSucceeded,
                new StockReservationSucceededPayload(order.OrderId, reservationId), envelope.CorrelationId);
            repository.AddOutboxMessage(OutboxModelBuilderExtensions.CreateOutboxMessage(succeeded, Topology.RoutingKeys.StockReservationSucceeded));
        }

        await repository.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var outcome = failedProducts.Count > 0 ? ReservationOutcome.Failed : ReservationOutcome.Succeeded;
        logger.LogInformation("Stock reservation {Outcome} for {OrderId} {MessageId} {CorrelationId}",
            outcome, order.OrderId, envelope.MessageId, envelope.CorrelationId);
        return outcome;
    }

    /// <summary>
    /// Her birim ayrı satırdır; müsait satırlar SKIP LOCKED ile alındığı için eşzamanlı siparişler birbirini beklemez.
    /// Kilitli satırlar yüzünden eksik kalınırsa (sahibi geri alabilir) kısa süre sonra tekrar denenir;
    /// yalnızca kilitliler dahil müsait toplam yetersizse sipariş başarısız sayılır.
    /// Boş liste dönerse tüm kalemler alınmıştır.
    /// </summary>
    private async Task<List<Guid>> ClaimUnitsAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, Guid reservationId,
        List<(Guid ProductId, int Quantity)> items, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await tx.CreateSavepointAsync("reserve", ct);

            var shortItems = new List<(Guid ProductId, int Quantity)>();
            foreach (var item in items)
            {
                var (productId, quantity) = item;
                var claimed = await repository.ClaimUnitsAsync(reservationId, productId, quantity, ct);
                if (claimed < quantity)
                {
                    shortItems.Add(item);
                    break;
                }
            }

            if (shortItems.Count == 0)
            {
                return [];
            }

            await tx.RollbackToSavepointAsync("reserve", ct);

            var insufficient = new List<Guid>();
            foreach (var (productId, quantity) in items)
            {
                var free = await repository.CountFreeUnitsAsync(productId, ct);
                if (free < quantity)
                {
                    insufficient.Add(productId);
                }
            }

            if (insufficient.Count > 0)
            {
                return insufficient;
            }

            if (attempt >= MaxClaimAttempts)
            {
                throw new InvalidOperationException(
                    $"Stock units for reservation {reservationId} stayed locked by concurrent orders; message will be retried.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
        }
    }
}
