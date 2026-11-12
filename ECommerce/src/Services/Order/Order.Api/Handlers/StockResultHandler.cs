using Messaging;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Api.Infrastructure;
using OrderEntity = Order.Api.Domain.Order;

namespace Order.Api.Handlers;

public enum ResultOutcome
{
    Applied,
    Duplicate,
    Ignored
}

public sealed class StockResultHandler(IOrderRepository repository, ILogger<StockResultHandler> logger)
{
    public Task<ResultOutcome> HandleSucceededAsync(EventEnvelope<StockReservationSucceededPayload> envelope, CancellationToken ct) =>
        ApplyAsync(envelope.MessageId, envelope.EventType, envelope.CorrelationId, envelope.Payload.OrderId,
            order => order.Confirm(),
            order => (Topology.RoutingKeys.OrderConfirmed,
                OutboxModelBuilderExtensions.CreateOutboxMessage(
                    EventEnvelope<OrderConfirmedPayload>.Create(EventTypes.OrderConfirmed,
                        new OrderConfirmedPayload(order.Id, order.CustomerId), envelope.CorrelationId),
                    Topology.RoutingKeys.OrderConfirmed)),
            ct);

    public Task<ResultOutcome> HandleFailedAsync(EventEnvelope<StockReservationFailedPayload> envelope, CancellationToken ct) =>
        ApplyAsync(envelope.MessageId, envelope.EventType, envelope.CorrelationId, envelope.Payload.OrderId,
            order => order.Reject(),
            order => (Topology.RoutingKeys.OrderRejected,
                OutboxModelBuilderExtensions.CreateOutboxMessage(
                    EventEnvelope<OrderRejectedPayload>.Create(EventTypes.OrderRejected,
                        new OrderRejectedPayload(order.Id, order.CustomerId, envelope.Payload.ReasonCode), envelope.CorrelationId),
                    Topology.RoutingKeys.OrderRejected)),
            ct);

    private async Task<ResultOutcome> ApplyAsync(
        Guid messageId, string eventType, Guid correlationId, Guid orderId,
        Func<OrderEntity, bool> transition,
        Func<OrderEntity, (string RoutingKey, OutboxMessage Message)> buildEvent,
        CancellationToken ct)
    {
        await using var tx = await repository.BeginTransactionAsync(ct);

        if (!await repository.TryRegisterInboxMessageAsync(messageId, eventType, ct))
        {
            logger.LogInformation("Duplicate message {MessageId} ignored", messageId);
            return ResultOutcome.Duplicate;
        }

        // Satır kilidi: aynı sipariş için eşzamanlı sonuçlar sırayla işlenir.
        var order = await repository.GetForUpdateAsync(orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found for message {messageId}.");

        if (!transition(order))
        {
            await tx.CommitAsync(ct);
            logger.LogWarning("Order {OrderId} is already {Status}; {EventType} {MessageId} ignored",
                orderId, order.Status, eventType, messageId);
            return ResultOutcome.Ignored;
        }

        repository.AddOutboxMessage(buildEvent(order).Message);
        await repository.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Order {OrderId} is now {Status} {MessageId} {CorrelationId}",
            orderId, order.Status, messageId, correlationId);
        return ResultOutcome.Applied;
    }
}
